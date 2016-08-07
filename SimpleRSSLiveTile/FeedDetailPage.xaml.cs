﻿using SimpleRSSLiveTile.ViewModels;
using SimpleRSSLiveTile.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel.Resources;

namespace SimpleRSSLiveTile
{

    public sealed partial class FeedDetailPage : Page
    {
        private static DependencyProperty s_itemProperty
            = DependencyProperty.Register("Feed", typeof(FeedViewModel), typeof(FeedDetailPage), new PropertyMetadata(null));

        private FeedDataSource feedDB = new FeedDataSource();

        public static DependencyProperty ItemProperty
        {
            get { return s_itemProperty; }
        }

        public FeedViewModel Feed
        {
            get { return (FeedViewModel)GetValue(s_itemProperty); }
            set { SetValue(s_itemProperty, value); }
        }

        public FeedDetailPage()
        {
            this.InitializeComponent();
        }

        //Parses the fields of the page and builds matching Feed item.
        private async Task<Feed> BuildFeedFromPage()
        {
            String tileXML = customTileXMLContent.Text;

            //Use default XML if there's nothing in the textbox
            if (tileXML== "")
            {
                ResourceLoader rl = new ResourceLoader();
                tileXML = rl.GetString("AdaptiveTemplate");
            }
            
            Feed feedToSave = new Feed(Feed.Id, "New RSS Feed", feedInput.Text, tileXML);
            String feedTitle= await feedToSave.getFeedTitle();

            feedToSave.setTitle(feedTitle);

            return feedToSave;

        }

        //Build a feed object with all the data we have, check if the XML and URL are correct, and save it to the DB.
        //This doesn't pin the feed to the start menu.
        private async void SaveFeed(object sender, RoutedEventArgs e)
        {
            Progress.Visibility = Windows.UI.Xaml.Visibility.Visible;

            //Test Feed.
            Feed f = await BuildFeedFromPage();

            //Test XML.
            Boolean validXML = f.testTileXML();
            if (!validXML)
            {
                greetingOutput.Foreground = new SolidColorBrush(Windows.UI.Colors.Red);
                greetingOutput.Text = "Invalid XML.";
                Progress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }
            
            if (f.isTileValid())
            {
                //We're good, save the feed and enable pinning.
                feedDB.SetFeed(f);

                

                greetingOutput.Text = "Feed Saved ! You can now pin it.";

                if (f.isTileValid())
                { 
                    if (f.isTilePinned())
                        unpinButton.Visibility = Visibility.Visible;
                    else
                        pinButton.Visibility = Visibility.Visible;
                }

                greetingOutput.Foreground = new SolidColorBrush(Windows.UI.Colors.Green);
                Progress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            else
            {
                greetingOutput.Foreground = new SolidColorBrush(Windows.UI.Colors.Red);
                greetingOutput.Text = "Invalid RSS Feed.";
                Progress.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        private void PinFeed(object sender, RoutedEventArgs e)
        {

            //this.RegisterBackgroundTask();

        }

        private void UnpinFeed(object sender, RoutedEventArgs e)
        {

        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null)
            {
                if (toggleSwitch.IsOn == true)
                {
                    //We set the XML this way since linebreaks fuck up data binding
                    customTileXMLContent.Text = Feed.TileXML;
                    customTileXML.Visibility = Visibility.Visible;
                }
                else
                {
                    customTileXML.Visibility = Visibility.Collapsed;
                    customTileXMLContent.Text = "";
                }
            }
        }

        private void Show_FormattingHelp(object sender, TappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Parameter is feed ID
            Feed = FeedViewModel.FromFeed(feedDB.GetFeedById((int)e.Parameter));

            var backStack = Frame.BackStack;
            var backStackCount = backStack.Count;

            if (backStackCount > 0)
            {
                var masterPageEntry = backStack[backStackCount - 1];
                backStack.RemoveAt(backStackCount - 1);

                // Doctor the navigation parameter for the master page so it
                // will show the correct item in the side-by-side view.
                var modifiedEntry = new PageStackEntry(
                    masterPageEntry.SourcePageType,
                    Feed.Id,
                    masterPageEntry.NavigationTransitionInfo
                    );
                backStack.Add(modifiedEntry);
            }

            // Register for hardware and software back request from the system
            SystemNavigationManager systemNavigationManager = SystemNavigationManager.GetForCurrentView();
            systemNavigationManager.BackRequested += DetailPage_BackRequested;
            systemNavigationManager.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            SystemNavigationManager systemNavigationManager = SystemNavigationManager.GetForCurrentView();
            systemNavigationManager.BackRequested -= DetailPage_BackRequested;
            systemNavigationManager.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
        }

        private void OnBackRequested()
        {
            // Page above us will be our master view.
            // Make sure we are using the "drill out" animation in this transition.
            if (Frame.BackStackDepth > 0)
                Frame.GoBack(new DrillInNavigationTransitionInfo());
        }

        void NavigateBackForWideState(bool useTransition)
        {
            // Evict this page from the cache as we may not need it again.
            NavigationCacheMode = NavigationCacheMode.Disabled;

            if (Frame.BackStackDepth >0)
                if (useTransition)
                {
                    Frame.GoBack(new EntranceNavigationTransitionInfo());
                }
                else
                {
                    Frame.GoBack(new SuppressNavigationTransitionInfo());
                }
        }

        private bool ShouldGoToWideState()
        {
            return Window.Current.Bounds.Width >= 720;
        }

        private void PageRoot_Loaded(object sender, RoutedEventArgs e)
        {
            if (ShouldGoToWideState() && Frame.BackStackDepth > 0)
            {
                // We shouldn't see this page since we are in "wide master-detail" mode. 
                //However, if it has been loaded as an embedded frame (no backstack), we'll proceed as usual.

                // Play a transition as we are navigating from a separate page.
                NavigateBackForWideState(useTransition: true);
            }
            else
            {
                // Realize the main page content.
                FindName("RootPanel");

                ResourceLoader rl = new ResourceLoader();
                if (Feed.TileXML != rl.GetString("AdaptiveTemplate"))
                {
                    customTileToggle.IsOn = true;
                    customTileXMLContent.Text = Feed.TileXML;
                    customTileXML.Visibility = Visibility.Visible;
                }

            }

            Window.Current.SizeChanged += Window_SizeChanged;
        }

        private void PageRoot_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged -= Window_SizeChanged;
        }

        private void Window_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            if (ShouldGoToWideState())
            {
                // Make sure we are no longer listening to window change events.
                Window.Current.SizeChanged -= Window_SizeChanged;

                // We shouldn't see this page since we are in "wide master-detail" mode.
                NavigateBackForWideState(useTransition: false);
            }
        }

        private void DetailPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            // Mark event as handled so we don't get bounced out of the app.
            e.Handled = true;

            OnBackRequested();
        }
    }
}