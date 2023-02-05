﻿using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.MapTemplates.Enums;
using AnnoMapEditor.MapTemplates.Models;
using AnnoMapEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace AnnoMapEditor.UI.Overlays.SelectSlots
{
    public delegate void FilterUpdatedEventHandler();


    public class SlotPositionComparer : IComparer<Vector2>
    {
        private static readonly Vector NORMAL = new(1, -1);

        private readonly double _islandHalfSize;

        private readonly Vector _islandCenter;


        public SlotPositionComparer(int islandSize)
        {
            _islandHalfSize = islandSize / 2d;
            _islandCenter = new(_islandHalfSize, _islandHalfSize);
        }


        public int Compare(Vector2? a, Vector2? b)
        {
            if (a == null || b == null)
                throw new ArgumentNullException();

            Vector aToCenter = new(a.X - _islandCenter.X, a.Y - _islandCenter.Y);
            Vector bToCenter = new(b.X - _islandCenter.X, b.Y - _islandCenter.Y);

            double aAngle = Vector.AngleBetween(aToCenter, NORMAL);
            double bAngle = Vector.AngleBetween(bToCenter, NORMAL);


            if (Math.Abs(aAngle - bAngle) < 10 && Math.Abs(aToCenter.Length - bToCenter.Length) > 50)
                return bToCenter.Length.CompareTo(aToCenter.Length);

            if (aAngle < bAngle)
                return 1;

            else if (aAngle > bAngle)
                return -1;

            else
                return bToCenter.Length.CompareTo(aToCenter.Length);
        }
    }


    public class SelectSlotsViewModel : ObservableBase, IOverlayViewModel
    {
        public IEnumerable<Region?> Regions { get; init; } = Region.All;

        private readonly Region _initialRegion;

        public Region SelectedRegion
        {
            get => _selectedRegion;
            set
            {
                _selectedRegion = value;
                UpdateFilter();

                ShowRegionWarning = _selectedRegion != _initialRegion;
            }
        }
        private Region _selectedRegion;

        public bool ShowRegionWarning
        {
            get => _showRegionWarning;
            set => SetProperty(ref _showRegionWarning, value);
        }
        private bool _showRegionWarning = false;

        public FixedIslandElement FixedIsland { get; init; }

        public bool ShowMines
        {
            get => _showMines;
            set
            {
                SetProperty(ref _showMines, value);
                UpdateFilter();
            }
        }
        private bool _showMines = true;

        public bool ShowClay
        {
            get => _showClay;
            set
            {
                SetProperty(ref _showClay, value);
                UpdateFilter();
            }
        }
        private bool _showClay = true;

        public bool ShowOil
        {
            get => _showOil;
            set
            {
                SetProperty(ref _showOil, value);
                UpdateFilter();
            }
        }
        private bool _showOil = true;

        public ObservableCollection<SlotAssignmentViewModel> SlotAssignmentViewModels { get; init; }
        public IEnumerable<SlotAssignmentViewModel> SlotAssignmentViewModelsLeft { get; init; }
        public IEnumerable<SlotAssignmentViewModel> SlotAssignmentViewModelsRight { get; init; }


        public SelectSlotsViewModel(Region region, FixedIslandElement fixedIsland)
        {
            _selectedRegion = _initialRegion = region;
            FixedIsland = fixedIsland;

            SlotAssignmentViewModels = new(
                fixedIsland.SlotAssignments.Values
                    .Where(s => s.Slot.SlotAsset != null)
                    .Select(s => new SlotAssignmentViewModel(s, region))
                    .OrderBy(s => s.SlotAssignment.Slot.Position, new SlotPositionComparer(fixedIsland.IslandAsset.SizeInTiles))
                );

            List<SlotAssignmentViewModel> left = new();
            Stack<SlotAssignmentViewModel> right = new();
            foreach (SlotAssignmentViewModel slot in SlotAssignmentViewModels)
            {
                Vector2 slotPosition = slot.SlotAssignment.Slot.Position;

                if (slotPosition.Y > -slotPosition.X + fixedIsland.IslandAsset.SizeInTiles)
                    left.Add(slot);
                else
                    right.Push(slot);
            }

            SlotAssignmentViewModelsLeft = left;
            SlotAssignmentViewModelsRight = right.ToList();

            CollectionView slotsView = (CollectionView)CollectionViewSource.GetDefaultView(SlotAssignmentViewModels);
            CollectionView slotsLeftView = (CollectionView)CollectionViewSource.GetDefaultView(SlotAssignmentViewModelsLeft);
            CollectionView slotsRightView = (CollectionView)CollectionViewSource.GetDefaultView(SlotAssignmentViewModelsRight);
            slotsView.Filter = SlotFilter;
            slotsLeftView.Filter = SlotFilter;
            slotsRightView.Filter = SlotFilter;
        }


        private bool SlotFilter(object item)
        {
            if (item is not SlotAssignmentViewModel slotAssignment)
                throw new ArgumentException();

            long slotGroupId = slotAssignment.SlotAssignment.Slot.ObjectGuid;

            if (!ShowMines && (
                  slotGroupId == SlotAsset.RANDOM_MINE_OLD_WORLD_GUID
               || slotGroupId == SlotAsset.RANDOM_MINE_NEW_WORLD_GUID
               || slotGroupId == SlotAsset.RANDOM_MINE_ARCTIC_GUID))
                return false;

            if (!ShowClay && slotGroupId == SlotAsset.RANDOM_CLAY_GUID)
                return false;

            if (!ShowOil && slotGroupId == SlotAsset.RANDOM_OIL_GUID)
                return false;

            return true;
        }

        private void UpdateFilter()
        {
            CollectionViewSource.GetDefaultView(SlotAssignmentViewModels).Refresh();
            CollectionViewSource.GetDefaultView(SlotAssignmentViewModelsLeft).Refresh();
            CollectionViewSource.GetDefaultView(SlotAssignmentViewModelsRight).Refresh();

            foreach (SlotAssignmentViewModel slotAssignment in SlotAssignmentViewModels)
                slotAssignment.SelectedRegion = _selectedRegion;
        }

        public void OnClosed()
        {
            OverlayService.Instance.Close(this);
        }
    }
}
