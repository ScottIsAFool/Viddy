﻿using Cimbalino.Toolkit.Services;
using GalaSoft.MvvmLight.Command;
using Viddy.Services;
using Viddy.ViewModel.Item;
using VidMePortable;

namespace Viddy.ViewModel.Account
{
    public class EditProfileViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IVidMeClient _vidMeClient;

        public EditProfileViewModel(INavigationService navigationService, IVidMeClient vidMeClient, AvatarViewModel avatarViewModel)
        {
            Avatar = avatarViewModel;
            _navigationService = navigationService;
            _vidMeClient = vidMeClient;
        }

        public AvatarViewModel Avatar { get; set; }

        public string Name { get; set; }
        public string Bio { get; set; }
        public string Email { get; set; }

        public string NewPassword { get; set; }
        public string CurrentPassword { get; set; }

        public RelayCommand PageLoadedCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    var user = AuthenticationService.Current.AuthenticationInfo.User;
                    Name = user.Username;
                    Bio = user.Bio;
                }, () => AuthenticationService.Current.IsLoggedIn);
            }
        }
    }
}
