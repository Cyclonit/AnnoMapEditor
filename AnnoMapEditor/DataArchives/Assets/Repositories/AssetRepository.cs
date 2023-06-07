﻿using AnnoMapEditor.DataArchives.Assets.Deserialization;
using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AnnoMapEditor.DataArchives.Assets.Repositories
{
    public class AssetRepository : Repository
    {
        private static readonly Logger<AssetRepository> _logger = new();

        private const string AssetsXmlPath = "data/config/export/main/asset/assets.xml";


        private readonly IDataArchive _dataArchive;

        private readonly GuidReferenceResolverFactory _guidReferenceResolverFactory;
        
        private readonly Dictionary<string, Func<XElement, StandardAsset>> _deserializers = new();

        private readonly Dictionary<Type, List<Action<object>>> _referenceResolvers = new();

        private readonly Dictionary<long, StandardAsset> _assets = new();

        private readonly List<Type> _assetTypes = new();


        public AssetRepository(IDataArchive dataArchive)
        {
            _dataArchive = dataArchive;
            _guidReferenceResolverFactory = new(this);
        }


        protected override Task DoLoad()
        {
            _logger.LogInformation($"Loading assets...");

            // load assets.xml
            using Stream assetsXmlStream = _dataArchive.OpenRead(AssetsXmlPath)
                ?? throw new Exception($"Could not locate assets.xml.");
            XDocument assetsXml = XDocument.Load(assetsXmlStream);

            // get all assets
            List<XElement> assetElements = assetsXml.Descendants("Asset").ToList();

            foreach (XElement assetElement in assetsXml.Descendants("Asset"))
            {
                // determine the type of template
                string? templateName = assetElement.Element("Template")?.Value;

                // deserialize the asset
                if (templateName != null && _deserializers.TryGetValue(templateName, out var deserializer))
                {
                    // deserialize the asset
                    XElement valuesElement = assetElement.Element("Values")!;

                    StandardAsset asset;
                    try
                    {
                        asset = deserializer(valuesElement);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Could not deserialize asset of template '{templateName}'.", ex);
                        continue;
                    }

                    // load icons
                    if (asset.IconFilename != null)
                        asset.Icon = _dataArchive.TryLoadIcon(asset.IconFilename);

                    _assets.Add(asset.GUID, asset);
                }
            }

            _logger.LogInformation($"Resolving asset references...");

            // resolve references
            foreach (StandardAsset asset in _assets.Values)
            {
                if (_referenceResolvers.TryGetValue(asset.GetType(), out List<Action<object>>? resolvers))
                    foreach (Action<object> resolver in resolvers)
                        resolver(asset);
            }

            _logger.LogInformation($"Resolving static asset references...");
            InitializeStaticAssets();

            _logger.LogInformation($"Finished loading {_assets.Count} assets.");
            return Task.CompletedTask;
        }

        public TAsset Get<TAsset>(long guid)
            where TAsset : StandardAsset
        {
            return (TAsset)_assets[guid];
        }

        public StandardAsset Get(long guid)
        {
            return _assets[guid];
        }

        public bool TryGet<TAsset>(long guid, [MaybeNullWhen(false)] out TAsset? asset)
            where TAsset : StandardAsset
        {
            if (_assets.TryGetValue(guid, out StandardAsset? standardAsset) && standardAsset is TAsset castAsset)
            {
                asset = castAsset;
                return true;
            }
            else
            {
                asset = null;
                return false;
            }
        }

        public bool TryGet(long guid, [MaybeNullWhen(false)] out StandardAsset? asset)
        {
            if (_assets.TryGetValue(guid, out StandardAsset? standardAsset))
            {
                asset = standardAsset;
                return true;
            }
            else
            {
                asset = null;
                return false;
            }
        }

        public IEnumerable<TAsset> GetAll<TAsset>()
            where TAsset : StandardAsset
        {
            foreach (StandardAsset asset in _assets.Values)
            {
                if (asset is TAsset castAsset)
                    yield return castAsset;
            }
        }

        public void Register<TAsset>()
            where TAsset : StandardAsset
        {
            AssetTemplateAttribute assetTemplateAttribute = typeof(TAsset).GetCustomAttribute<AssetTemplateAttribute>()
                ?? throw new Exception($"Cannot register type '{typeof(TAsset).FullName}' as an asset model, because it lacks the {nameof(AssetTemplateAttribute)}.");
            string templateName = assetTemplateAttribute.TemplateName;

            // get the deserializer
            ConstructorInfo deserializerConstructor = typeof(TAsset).GetConstructor(new[] { typeof(XElement) })
                ?? throw new Exception($"Type {typeof(TAsset).FullName} is not a valid asset model. Asset models must have a deserialization constructor.");
            Func<XElement, TAsset> deserializer = (x) => (TAsset)deserializerConstructor.Invoke(new[] { x });

            _deserializers.Add(templateName, deserializer);

            // prepare to resolve references
            foreach (PropertyInfo property in typeof(TAsset).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Action<object>? resolver = _guidReferenceResolverFactory.CreateResolver<TAsset>(property);

                // keep track of all resolvers
                if (resolver != null)
                {
                    if (!_referenceResolvers.TryGetValue(typeof(TAsset), out List<Action<object>>? resolvers))
                    {
                        resolvers = new();
                        _referenceResolvers.Add(typeof(TAsset), resolvers);
                    }
                    resolvers.Add(resolver);
                }
            }

            _assetTypes.Add(typeof(TAsset));

            _logger.LogInformation($"Registered asset type '{typeof(TAsset).FullName}'.");
        }

        private StandardAsset? GetReferencedAsset(long guid, Type referencedType)
        {
            if (TryGet(guid, out StandardAsset? referencedAsset))
            {
                if (referencedType.IsAssignableFrom(referencedAsset!.GetType()))
                    return referencedAsset;
                else
                    _logger.LogWarning($"Could not resolve reference GUID {guid} to type {referencedType.FullName}. The referenced asset has non-matching type {referencedAsset.GetType()}.");
            }
            else
                _logger.LogWarning($"Could not resolve reference GUID {guid} to type {referencedType.FullName}. No asset with that GUID could be found.");

            return null;
        }

        private void InitializeStaticAssets()
        {
            foreach (Type assetType in _assetTypes)
            {
                foreach (PropertyInfo staticProperty in assetType.GetProperties(BindingFlags.Static | BindingFlags.Public))
                {
                    StaticAssetAttribute? staticAssetAttribute = staticProperty.GetCustomAttribute<StaticAssetAttribute>();
                    if (staticAssetAttribute == null)
                        continue;

                    if (TryGet(staticAssetAttribute.GUID, out StandardAsset? asset))
                    {
                        if (!staticProperty.PropertyType.IsAssignableFrom(asset.GetType()))
                            throw new Exception($"Could not resolve StaticAsset {assetType.FullName}.{staticProperty.Name}. The asset's type {asset.GetType().FullName} does not match the property's type {staticProperty.PropertyType.FullName}.");

                        staticProperty.SetValue(null, asset);
                    }
                    else
                        throw new Exception($"Could not resolve StaticAsset {assetType.FullName}.{staticProperty.Name}. There exists no asset with GUID {staticAssetAttribute.GUID}.");
                }
            }
        }
    }
}
