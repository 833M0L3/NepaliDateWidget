﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using OpenNetMeter.Models;

namespace OpenNetMeter.ViewModels
{
    public class MainWindowVM : INotifyPropertyChanged, IDisposable
    {
        private readonly DataUsageSummaryVM dusvm;
        private readonly DataUsageDetailedVM dudvm;
        private readonly DataUsageHistoryVM duhvm;
        private readonly MiniWidgetVM mwvm;
        private readonly SettingsVM svm;
        private readonly NetworkProcess netProc;
        public ICommand SwitchTabCommand { get; set; }

        private int tabBtnToggle;
        public int TabBtnToggle
        {
            get { return tabBtnToggle; }
            set { tabBtnToggle = value; OnPropertyChanged("TabBtnToggle"); }
        }

        private object? selectedViewModel;
        public object? SelectedViewModel
        {
            get { return selectedViewModel; }
            set { selectedViewModel = value; OnPropertyChanged("SelectedViewModel"); }
        }
        public ulong downloadSpeed;
        public ulong DownloadSpeed
        {
            get { return downloadSpeed; }
            set { downloadSpeed = value; OnPropertyChanged("DownloadSpeed"); }
        }
        public ulong uploadSpeed;
        public ulong UploadSpeed
        {
            get { return uploadSpeed; }
            set { uploadSpeed = value; OnPropertyChanged("UploadSpeed"); }
        }

        private string networkStatus;
        public string NetworkStatus
        {
            get { return networkStatus; }
            set { networkStatus = value; OnPropertyChanged("NetworkStatus"); }
        }

        private enum TabPage
        {
            Summary,
            Detailed,
            History,
            Settings
        }

        public MainWindowVM(MiniWidgetVM mwvm_DataContext, ConfirmationDialogVM cD_DataContext) //runs once during app init
        {
            DownloadSpeed = 0;
            UploadSpeed = 0;

            networkStatus = "";

            mwvm = mwvm_DataContext;
            dusvm = new DataUsageSummaryVM();
            duhvm = new DataUsageHistoryVM();
            dudvm = new DataUsageDetailedVM(cD_DataContext);
            svm = new SettingsVM();

            netProc = new NetworkProcess();
            netProc.PropertyChanged += NetProc_PropertyChanged;
            netProc.Initialize();

            //---- sample db section --------//
            Stopwatch sw = new Stopwatch();
            sw.Start();


            //using (ApplicationDB dB = new ApplicationDB("WiFi 4"))
            //{
            //    if (dB.CreateTable() < 0)
            //        Debug.WriteLine("Error: Create table");
            //    else
            //    {
            //        Debug.WriteLine("Success: Create table");
            //        dB.InsertUniqueRow_ProcessTable("app1");
            //        dB.InsertUniqueRow_ProcessTable("app1");
            //        dB.InsertUniqueRow_ProcessTable("app2");

            //        //populate date table with past 60 days
            //        //dB.BulkInsertDateRange_DateTable(DateTime.Now, -60);

            //        dB.InsertUniqueRow_DateTable(new DateTime(2021, 03, 15));
            //        dB.InsertUniqueRow_DateTable(new DateTime(2021, 03, 15));
            //        dB.InsertUniqueRow_DateTable(new DateTime(2021, 11, 21));
            //        dB.InsertUniqueRow_DateTable(new DateTime(2022, 06, 10));
            //        dB.InsertUniqueRow_DateTable(new DateTime(2022, 06, 08));
            //        dB.InsertUniqueRow_DateTable(new DateTime(2022, 06, 02));
            //        dB.RemoveOldDates();

            //        long dateID = dB.GetID_DateTable(new DateTime(2022, 06, 08));
            //        long processID = dB.GetID_ProcessTable("app2");
            //        //example data insert to processDate table
            //        Debug.WriteLine("date Id: " + dateID);
            //        Debug.WriteLine("Process Id: " + processID);

            //        if (dB.InsertUniqueRow_ProcessDateTable(processID, dateID, 2056, 123) < 1)
            //        {
            //            dB.UpdateRow_ProcessDateTable(processID, dateID, 2056, 123);
            //        }

            //    }
            //}
                
            sw.Stop();
            Debug.WriteLine("time: " + sw.ElapsedMilliseconds);

            //intial startup page
            TabBtnToggle = Properties.Settings.Default.LaunchPage;
            switch (TabBtnToggle)
            {
                case ((int)TabPage.Summary):
                    SelectedViewModel = dusvm;
                    break;
                case ((int)TabPage.Detailed):
                    SelectedViewModel = dudvm;
                    break;
                case ((int)TabPage.History):
                    SelectedViewModel = duhvm;
                    break;
                case ((int)TabPage.Settings):
                    SelectedViewModel = svm;
                    break;
                default:
                    SelectedViewModel = dusvm;
                    TabBtnToggle = ((int)TabPage.Summary);
                    break;
            }

            //assign basecommand
            SwitchTabCommand = new BaseCommand(SwitchTab);
        }

        private void NetProc_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "DownloadSpeed":
                    //update download and upload together, why? because they update concurrently
                    DownloadSpeed = netProc.DownloadSpeed;
                    mwvm.DownloadSpeed = DownloadSpeed;
                    UploadSpeed = netProc.UploadSpeed;
                    mwvm.UploadSpeed = UploadSpeed;

                    dusvm.CurrentSessionDownloadData = netProc.CurrentSessionDownloadData;
                    dusvm.CurrentSessionUploadData = netProc.CurrentSessionUploadData;
                    break;
                case "IsNetworkOnline":
                    NetworkStatus = netProc.IsNetworkOnline;
                    break;
                default:
                    break;
            }
        }

        private void SwitchTab(object obj)
        {
            string? tab = obj as string;
            switch (tab)
            {
                case "summary":
                    if (TabBtnToggle != ((int)TabPage.Summary))
                    {
                        SelectedViewModel = dusvm;
                        TabBtnToggle = ((int)TabPage.Summary);
                        Properties.Settings.Default.LaunchPage = TabBtnToggle;
                        Properties.Settings.Default.Save();
                    }
                    break;
                case "detailed":
                    if (TabBtnToggle != ((int)TabPage.Detailed))
                    {
                        SelectedViewModel = dudvm;
                        TabBtnToggle = ((int)TabPage.Detailed);
                        Properties.Settings.Default.LaunchPage = TabBtnToggle;
                        Properties.Settings.Default.Save();
                    }
                    break;
                case "history":
                    if (TabBtnToggle != ((int)TabPage.History))
                    {
                        SelectedViewModel = duhvm;
                        TabBtnToggle = ((int)TabPage.History);
                        Properties.Settings.Default.LaunchPage = TabBtnToggle;
                        Properties.Settings.Default.Save();
                    }
                    break;
                case "settings":
                    if (TabBtnToggle != ((int)TabPage.Settings))
                    {
                        SelectedViewModel = svm;
                        TabBtnToggle = ((int)TabPage.Settings);
                        Properties.Settings.Default.LaunchPage = TabBtnToggle;
                        Properties.Settings.Default.Save();
                    }
                    break;
                default:
                    break;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }

        public void Dispose()
        {
            if(netProc != null)
                netProc.Dispose();
        }
    }
}
