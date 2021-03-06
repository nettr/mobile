﻿using System;
using Bit.App.Abstractions;
using Bit.App.Controls;
using Bit.App.Resources;
using Plugin.Connectivity.Abstractions;
using Xamarin.Forms;
using XLabs.Ioc;
using System.Linq;
using Bit.App.Utilities;

namespace Bit.App.Pages
{
    public class SettingsEditFolderPage : ExtendedContentPage
    {
        private readonly string _folderId;
        private readonly IFolderService _folderService;
        private readonly IDeviceActionService _deviceActionService;
        private readonly IConnectivity _connectivity;
        private readonly IGoogleAnalyticsService _googleAnalyticsService;
        private DateTime? _lastAction;

        public SettingsEditFolderPage(string folderId)
        {
            _folderId = folderId;
            _folderService = Resolver.Resolve<IFolderService>();
            _deviceActionService = Resolver.Resolve<IDeviceActionService>();
            _connectivity = Resolver.Resolve<IConnectivity>();
            _googleAnalyticsService = Resolver.Resolve<IGoogleAnalyticsService>();

            Init();
        }

        public FormEntryCell NameCell { get; set; }
        public ExtendedTextCell DeleteCell { get; set; }

        private void Init()
        {
            var folder = _folderService.GetByIdAsync(_folderId).GetAwaiter().GetResult();
            if(folder == null)
            {
                // TODO: handle error. navigate back? should never happen...
                return;
            }

            NameCell = new FormEntryCell(AppResources.Name);
            NameCell.Entry.Text = folder.Name.Decrypt();

            DeleteCell = new ExtendedTextCell { Text = AppResources.Delete, TextColor = Color.Red };

            var mainTable = new ExtendedTableView
            {
                Intent = TableIntent.Settings,
                HasUnevenRows = true,
                Root = new TableRoot
                {
                    new TableSection(Helpers.GetEmptyTableSectionTitle())
                    {
                        NameCell
                    },
                    new TableSection(Helpers.GetEmptyTableSectionTitle())
                    {
                        DeleteCell
                    }
                }
            };

            if(Device.RuntimePlatform == Device.iOS)
            {
                mainTable.RowHeight = -1;
                mainTable.EstimatedRowHeight = 70;
            }
            else if(Device.RuntimePlatform == Device.Android)
            {
                mainTable.BottomPadding = 50;
            }

            var saveToolBarItem = new ToolbarItem(AppResources.Save, Helpers.ToolbarImage("envelope.png"), async () =>
            {
                if(_lastAction.LastActionWasRecent())
                {
                    return;
                }
                _lastAction = DateTime.UtcNow;

                if(!_connectivity.IsConnected)
                {
                    AlertNoConnection();
                    return;
                }

                if(string.IsNullOrWhiteSpace(NameCell.Entry.Text))
                {
                    await DisplayAlert(AppResources.AnErrorHasOccurred, string.Format(AppResources.ValidationFieldRequired,
                        AppResources.Name), AppResources.Ok);
                    return;
                }

                folder.Name = NameCell.Entry.Text.Encrypt();

                await _deviceActionService.ShowLoadingAsync(AppResources.Saving);
                var saveResult = await _folderService.SaveAsync(folder);
                await _deviceActionService.HideLoadingAsync();

                if(saveResult.Succeeded)
                {
                    _deviceActionService.Toast(AppResources.FolderUpdated);
                    _googleAnalyticsService.TrackAppEvent("EditedFolder");
                    await Navigation.PopForDeviceAsync();
                }
                else if(saveResult.Errors.Count() > 0)
                {
                    await DisplayAlert(AppResources.AnErrorHasOccurred, saveResult.Errors.First().Message, AppResources.Ok);
                }
                else
                {
                    await DisplayAlert(null, AppResources.AnErrorHasOccurred, AppResources.Ok);
                }
            }, ToolbarItemOrder.Default, 0);

            Title = AppResources.EditFolder;
            Content = mainTable;
            ToolbarItems.Add(saveToolBarItem);
            if(Device.RuntimePlatform == Device.iOS || Device.RuntimePlatform == Device.UWP)
            {
                ToolbarItems.Add(new DismissModalToolBarItem(this, AppResources.Cancel));
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            NameCell.InitEvents();
            DeleteCell.Tapped += DeleteCell_Tapped;

            if(!_connectivity.IsConnected)
            {
                AlertNoConnection();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            NameCell.Dispose();
            DeleteCell.Tapped -= DeleteCell_Tapped;
        }

        private async void DeleteCell_Tapped(object sender, EventArgs e)
        {
            if(!_connectivity.IsConnected)
            {
                AlertNoConnection();
                return;
            }

            // TODO: Validate the delete operation. ex. Cannot delete a folder that has ciphers in it?

            var confirmed = await DisplayAlert(null, AppResources.DoYouReallyWantToDelete, AppResources.Yes, AppResources.No);
            if(!confirmed)
            {
                return;
            }

            await _deviceActionService.ShowLoadingAsync(AppResources.Deleting);
            var deleteTask = await _folderService.DeleteAsync(_folderId);
            await _deviceActionService.HideLoadingAsync();

            if(deleteTask.Succeeded)
            {
                _deviceActionService.Toast(AppResources.FolderDeleted);
                await Navigation.PopForDeviceAsync();
            }
            else if(deleteTask.Errors.Count() > 0)
            {
                await DisplayAlert(AppResources.AnErrorHasOccurred, deleteTask.Errors.First().Message, AppResources.Ok);
            }
            else
            {
                await DisplayAlert(null, AppResources.AnErrorHasOccurred, AppResources.Ok);
            }
        }

        private void AlertNoConnection()
        {
            DisplayAlert(AppResources.InternetConnectionRequiredTitle, AppResources.InternetConnectionRequiredMessage,
                AppResources.Ok);
        }
    }
}
