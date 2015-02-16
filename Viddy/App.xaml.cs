﻿using System;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Security.Authentication.Web;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Cimbalino.Toolkit.Services;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using Viddy.Common;
using Viddy.Core;
using Viddy.Core.Extensions;
using Viddy.Extensions;
using Viddy.Messaging;
using Viddy.Services;
using Viddy.ViewModel;
using Viddy.ViewModel.Item;
using Viddy.Views;
using Viddy.Views.Account;
using VidMePortable.Model;

namespace Viddy
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App
    {
        private TransitionCollection _transitions;

        public static ViewModelLocator Locator
        {
            get { return Current.Resources["Locator"] as ViewModelLocator; }
        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            e.Handled = true;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (Debugger.IsAttached)
            {
                DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            var rootFrame = Window.Current.Content as Frame;
            Messenger.Default.Send(new PinMessage());

            AppStarted();

            if (!string.IsNullOrEmpty(e.Arguments) && Uri.IsWellFormedUriString(e.Arguments, UriKind.Absolute) && e.Arguments.StartsWith("viddy://"))
            {
                var uri = new Uri(e.Arguments);
                if (!string.IsNullOrEmpty(uri.Host))
                {
                    HandleUriLaunch(uri);
                    return;
                }
            }

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                // TODO: change this value to a cache size that is appropriate for your application
                rootFrame.CacheSize = 10;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }
            else
            {
                if (!string.IsNullOrEmpty(e.Arguments))
                {
                    rootFrame.BackStack.Clear();
                }
            }

            var pageToLoad = PageToLoad(e.Arguments);

            if (rootFrame.Content == null)
            {
                // Removes the turnstile navigation for startup.
                if (rootFrame.ContentTransitions != null)
                {
                    _transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        _transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += RootFrame_FirstNavigated;

                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (!rootFrame.Navigate(pageToLoad, new NavigationParameters {ShowHomeButton = pageToLoad != typeof (MainView)}))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            if (e.TileId != "App")
            {
                rootFrame.Navigate(pageToLoad);
            }

            // Ensure the current window is active
            Window.Current.Activate();
        }

        private void AppStarted()
        {
            if (_appStarted) return;

            Window.Current.VisibilityChanged += CurrentOnVisibilityChanged;

            Locator.SettingsService.StartService();
            Locator.Auth.StartService();
            Locator.NotificationService.StartService();
            Locator.Review.IncreaseCount();
            Locator.TaskService.CreateService();
            _appStarted = true;
        }

        private bool _appStarted;

        private static void CurrentOnVisibilityChanged(object sender, VisibilityChangedEventArgs visibilityChangedEventArgs)
        {
            Messenger.Default.Send(new PinMessage());
        }

        public Type PageToLoad(string arguments)
        {
            var type = typeof(MainView);
            if (string.IsNullOrEmpty(arguments))
            {
                return type;
            }

            var query = new Uri(arguments).QueryString();
            if (!query.ContainsKey("tileType"))
            {
                return type;
            }

            var source = query["tileType"];
            var tileType = (TileService.TileType)Enum.Parse(typeof(TileService.TileType), source);
            var id = query["id"];

            var item = Locator.TileService.GetPinnedItemDetails(tileType, id);

            switch (tileType)
            {
                case TileService.TileType.VideoRecord:
                    type = typeof(VideoRecordView);
                    break;
                case TileService.TileType.Channel:
                    Messenger.Default.Send(new ChannelMessage(new ChannelItemViewModel((Channel)item)));
                    type = typeof(ChannelView);
                    break;
                case TileService.TileType.User:
                    Messenger.Default.Send(new UserMessage(new UserViewModel((User)item)));
                    type = typeof(ProfileView);
                    break;
                case TileService.TileType.Video:
                    Messenger.Default.Send(new VideoMessage(new VideoItemViewModel((Video)item, null)));
                    type = typeof(VideoPlayerView);
                    break;
                default:
                    type = typeof(MainView);
                    break;
            }

            return type;
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);

            AppStarted();

            if (args != null)
            {
                if (args.Kind == ActivationKind.WebAuthenticationBrokerContinuation)
                {
                    var brokerArgs = args as IWebAuthenticationBrokerContinuationEventArgs;
                    if (brokerArgs != null)
                    {
                        if (brokerArgs.WebAuthenticationResult.ResponseStatus == WebAuthenticationStatus.Success)
                        {
                            var url = brokerArgs.WebAuthenticationResult.ResponseData;
                            if (url.Contains(Constants.CallBackUrl))
                            {
                                var uri = new Uri(url);
                                var queryString = uri.QueryString();
                                var code = queryString["code"];
                                Messenger.Default.Send(new NotificationMessage(code, Constants.Messages.AuthCodeMsg));
                            }
                        }
                    }
                }
                else if (args.Kind == ActivationKind.PickFileContinuation)
                {
                    var pickerArgs = args as IFileOpenPickerContinuationEventArgs;
                    if (pickerArgs != null)
                    {
                        if (pickerArgs.Files.Any())
                        {
                            var file = pickerArgs.Files[0];
                            if (file.ContentType.ToLower().Contains("image"))
                            {
                                Messenger.Default.Send(new NotificationMessage(file, Constants.Messages.ProfileFileMsg));
                            }
                            else
                            {
                                SendFile(file);
                            }
                        }
                    }
                }
                else if (args.Kind == ActivationKind.Protocol)
                {
                    var eventArgs = args as ProtocolActivatedEventArgs;
                    if (eventArgs != null)
                    {
                        var uri = eventArgs.Uri;
                        HandleUriLaunch(uri);
                    }
                }
            }
        }

        private static void HandleUriLaunch(Uri uri)
        {
            // viddy://search?query={0}
            // viddy://record
            // viddy://
            // viddy://user?id={0}
            // viddy://channel?id={0}
            // viddy://video?id={0}

            Type pageToGoTo;
            var query = uri.QueryString();

            if (query.ContainsKey("notificationId"))
            {
                var notificationId = query["notificationId"];
                ToastNotificationManager.History.Remove(notificationId);
            }

            switch (uri.Host)
            {
                case "search":
                    pageToGoTo = typeof (SearchView);
                    var includeNsfw = false;
                    if (query.ContainsKey("nsfw"))
                    {
                        includeNsfw = bool.Parse(query["nsfw"]);
                    }

                    Messenger.Default.Send(new ProtocolMessage(ProtocolMessage.ProtocolType.Search, query["query"], includeNsfw));
                    break;
                case "record":
                    pageToGoTo = typeof (VideoRecordView);
                    break;
                case "user":
                    pageToGoTo = typeof (ProfileView);
                    Messenger.Default.Send(new ProtocolMessage(ProtocolMessage.ProtocolType.User, query["id"]));
                    break;
                case "channel":
                    pageToGoTo = typeof (ChannelView);
                    Messenger.Default.Send(new ProtocolMessage(ProtocolMessage.ProtocolType.Channel, query["id"]));
                    break;
                case "video":
                    pageToGoTo = typeof (VideoPlayerView);
                    Messenger.Default.Send(new ProtocolMessage(ProtocolMessage.ProtocolType.Video, query["id"]));
                    break;
                default:
                    pageToGoTo = typeof (MainView);
                    break;
            }

            var frame = new Frame();
            frame.Navigate(pageToGoTo, new NavigationParameters {ShowHomeButton = true});

            Window.Current.Content = frame;
            Window.Current.Activate();
        }

        private static void SendFile(IStorageItem file)
        {
            if (Locator.Upload != null)
            {
                Messenger.Default.Send(new NotificationMessage(file, Constants.Messages.VideoFileMsg));
                SimpleIoc.Default.GetInstance<INavigationService>().Navigate<UploadVideoView>();
            }
        }

        protected override async void OnShareTargetActivated(ShareTargetActivatedEventArgs args)
        {
            AppStarted();
            base.OnShareTargetActivated(args);
            var shareOperation = args.ShareOperation;
            if (shareOperation.Data.Contains(StandardDataFormats.StorageItems))
            {
                var files = await shareOperation.Data.GetStorageItemsAsync();
                if (files.Count > 1)
                {
                    // TODO: Display error message
                    shareOperation.ReportError("Viddy can only accept one file at a time");
                    return;
                }

                var file = files[0];

                var frame = new Frame();
                Window.Current.Content = frame;
                Window.Current.Activate();
                SendFile(file);
            }
        }

        /// <summary>
        /// Restores the content transitions after the app has launched.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the navigation event.</param>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = _transitions ?? new TransitionCollection { new NavigationThemeTransition() };
            rootFrame.Navigated -= RootFrame_FirstNavigated;
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            Window.Current.VisibilityChanged -= VideoRecordView.CurrentOnVisibilityChanged;
            Window.Current.VisibilityChanged -= CurrentOnVisibilityChanged;

            // TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}