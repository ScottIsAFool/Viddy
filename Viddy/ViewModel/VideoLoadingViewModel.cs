﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Cimbalino.Toolkit.Extensions;
using GalaSoft.MvvmLight.Command;
using Viddy.Extensions;
using Viddy.Model;
using Viddy.Services;
using VidMePortable.Model.Responses;

namespace Viddy.ViewModel
{
    public class VideoLoadingViewModel : ViewModelBase
    {
        private bool _videosLoaded;
        public bool CanLoadMore { get; set; }
        public bool IsLoadingMore { get; set; }
        public ObservableCollection<IListType> Items { get; set; }
        public bool IsEmpty { get; set; }

        public RelayCommand PageLoadedCommand
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    await PageLoaded();
                });
            }
        }

        public RelayCommand RefreshCommand
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    await LoadData(true);
                });
            }
        }

        public RelayCommand LoadMoreCommand
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    await LoadData(false, true, Items.Count);
                });
            }
        }

        public virtual async Task PageLoaded()
        {
            await LoadData(false);
        }

        public virtual async Task<VideosResponse> GetVideos(int offset)
        {
            return new VideosResponse();
        }

        protected void Reset()
        {
            Items = null;
            _videosLoaded = false;
            CanLoadMore = false;
            IsLoadingMore = false;
            IsEmpty = false;
        }

        protected async Task LoadData(bool isRefresh, bool add = false, int offset = 0)
        {
            if (_videosLoaded && !isRefresh && !add)
            {
                return;
            }

            try
            {
                if (!add)
                {
                    SetProgressBar("Getting videos...");
                }

                IsLoadingMore = add;
                var response = await GetVideos(offset);

                if (Items == null || !add)
                {
                    Items = new ObservableCollection<IListType>();
                }

                var allVideos = response.Videos.Select(x => new VideoItemViewModel(x, this)).ToList();

                if (IncludeReviewsInFeed() && !allVideos.IsNullOrEmpty())
                {
                    allVideos.AddEveryOften(10, 2, ReviewService.Current.ReviewViewModel);
                }

                //allVideos.AddEveryOften(10, 2, new AdViewModel(), 5, true);

                Items.AddRange(allVideos);

                IsEmpty = Items.IsNullOrEmpty();
                CanLoadMore = Items.Count + 1 < response.Page.Total;
                _videosLoaded = true;
            }
            catch (Exception ex)
            {

            }

            IsLoadingMore = false;
            SetProgressBar();
        }

        protected virtual bool IncludeReviewsInFeed()
        {
            return ReviewService.Current.CanShowReviews;
        }
    }
}
