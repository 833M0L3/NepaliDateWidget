﻿using System;
using System.Net;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using OpenNetMeter.ViewModels;
using System.Threading;
using ManagedNativeWifi;
using System.Net.Sockets;
using System.Collections.Generic;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Models
{
    public class NetworkProcess : IDisposable
    {
        //---------- private variables ------------//

        private byte[] defaultIPv4;
        private byte[] defaultIPv6;
        private byte[] localIPv4;
        private byte[] localIPv6;

        //token for network speed
        private CancellationTokenSource? cts_speed;
        private CancellationToken token_speed;

        private TraceEventSession? kernelSession;

        private string adapterName;

        private Dictionary<string, MyProcess>? myProcess;

        //---------- variables with property changers ------------//
        private ulong currentSessionDownloadData;
        public ulong CurrentSessionDownloadData
        {
            get { return currentSessionDownloadData; }
            set
            {
                currentSessionDownloadData = value;
                OnPropertyChanged("CurrentSessionDownloadData");
            }
        }
        private ulong currentSessionUploadData;
        public ulong CurrentSessionUploadData
        {
            get { return currentSessionUploadData; }
            set
            {
                currentSessionUploadData = value;
                OnPropertyChanged("CurrentSessionUploadData");
            }
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

        private string isNetworkOnline = "error";
        public string IsNetworkOnline
        {
            get { return isNetworkOnline; }
            set { isNetworkOnline = value; OnPropertyChanged("IsNetworkOnline"); }
        }
        public NetworkProcess()
        {
            //initialize variables
            defaultIPv4 = new byte[]
            {
                0, 0, 0, 0
            };
            defaultIPv6 = new byte[]
            {
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0
            };
            localIPv4 = defaultIPv4;
            localIPv6 = defaultIPv6;
            adapterName = "";
        }

        /// <summary>
        /// call after subscribing to the property handlers
        /// </summary>
        public void Initialize()
        {
            IsNetworkOnline = "Disconnected";
            currentSessionDownloadData = 0;
            currentSessionUploadData = 0;
            downloadSpeed = 0;
            uploadSpeed = 0;

            //subscribe address network address change
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;

            //capture network packets
            CaptureNetworkPackets();

            //check for online network addresses
            NetworkChange_NetworkAddressChanged(null, null);
        }

        /// <summary>
        /// returns local IP (IPv4, IPv6)
        /// </summary>
        /// <returns></returns>
        private (byte[], byte[]) GetLocalIP()
        {
            byte[] tempv4 = defaultIPv4;
            byte[] tempv6 = defaultIPv6;

            // IPv4
            using (Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, 0))
            {
                try
                {
                    socket.Connect("2001:4860:4860::8888", 65530);
                    IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
                    if(endPoint != null)
                        tempv6 = endPoint.Address.GetAddressBytes();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            // IPv6
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                try
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
                    if(endPoint != null)
                        tempv4 = endPoint.Address.GetAddressBytes();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            return (tempv4, tempv6);
        }

        private void NetworkChange_NetworkAddressChanged(object? sender, EventArgs? e)
        {
            (byte[], byte[]) tempIP = (defaultIPv4, defaultIPv6);
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface n in adapters)
            {
                if (n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    if (n.OperationalStatus == OperationalStatus.Up) //if there is a connection
                    {
                        tempIP = GetLocalIP(); //get assigned ip

                        IPInterfaceProperties adapterProperties = n.GetIPProperties();
                        if (adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                        {
                            bool networkAvailable = false;
                            foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                            {
                                if (ByteArray.Compare(ip.Address.GetAddressBytes(), tempIP.Item1))
                                {
                                    if (localIPv4 == tempIP.Item1) //this is to prevent this event from firing multiple times during 1 connection change
                                        break;
                                    else
                                        localIPv4 = tempIP.Item1;

                                    networkAvailable = true;
                                    
                                    Debug.WriteLine(n.Name + " is up " + ", IP: " + ip.Address.ToString());
                                }
                                else if(ByteArray.Compare(ip.Address.GetAddressBytes(), tempIP.Item2))
                                {
                                    if (localIPv6 == tempIP.Item2) //this is to prevent this event from firing multiple times during 1 connection change
                                        break;
                                    else
                                        localIPv6 = tempIP.Item2;

                                    networkAvailable = true;

                                    Debug.WriteLine(n.Name + " is up " + ", IP: " + ip.Address.ToString());
                                }
                            }
                            if(networkAvailable)
                            {
                                if (IsNetworkOnline != "Disconnected") //if there was already a connection available
                                    SetNetworkStatus(false); //reset the connection

                                adapterName = n.Name;

                                if (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                                    adapterName += "(" + NativeWifi.EnumerateConnectedNetworkSsids()?.FirstOrDefault()?.ToString() + ")";

                                SetNetworkStatus(true);
                            }
                        }
                    }
                }
            }

            //the ByteArrayCompare is used to detect virtual ethernet adapters escaping from a null,
            //adapterProperties.GatewayAddresses.FirstOrDefault() in the above foreach loop
            if (!NetworkInterface.GetIsNetworkAvailable() || ByteArray.Compare(tempIP.Item1, defaultIPv4))
            {
                localIPv4 = new byte[] { 0, 0, 0, 0 };
                Debug.WriteLine("No connection");
                if (isNetworkOnline != "Disconnected")
                    SetNetworkStatus(false);
            }
        }

        private void SetNetworkStatus(bool isOnline)
        {
            if (isOnline)
            {
                IsNetworkOnline = "Connected : " + adapterName;

                //create DB file if it does not exists
                using (ApplicationDB dB = new ApplicationDB(adapterName))
                {
                    if (dB.CreateTable() < 0)
                        Debug.WriteLine("Error: Create table");
                    else
                    {
                        //insert todays date
                        dB.InsertUniqueRow_DateTable(DateTime.Today);
                        //remove old entries
                        dB.RemoveOldEntries();
                    }
                }

                CaptureNetworkSpeed(); //start logging the speed
            }
            else //if network is disconnected
            {
                IsNetworkOnline = "Disconnected";

                if (cts_speed != null)
                    cts_speed.Cancel(); //stop calculating network speed

                //reset speed counters
                DownloadSpeed = 0;
                UploadSpeed = 0;
            }
        }

        private void CaptureNetworkSpeed()
        {
            cts_speed = new CancellationTokenSource();
            token_speed = cts_speed.Token;

            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("Operation Started : Network speed");
                    while (!token_speed.IsCancellationRequested)
                    {
                        ulong tempDownload = currentSessionDownloadData;
                        ulong tempUpload = currentSessionUploadData;
                        await Task.Delay(1000, token_speed);

                        DownloadSpeed = (currentSessionDownloadData - tempDownload) * 8;
                        UploadSpeed = (currentSessionUploadData - tempUpload) * 8;
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Operation Cancelled : Network speed");
                    cts_speed.Dispose();
                    cts_speed = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Critical error: " + ex.Message);
                }
            });
        }
        private void CaptureNetworkPackets()
        {
            Task.Run(() =>
            {
                kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
                if(kernelSession != null)
                {
                    kernelSession.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
                    kernelSession.Source.Kernel.TcpIpRecv += Kernel_TcpIpRecv;
                    kernelSession.Source.Kernel.TcpIpRecvIPV6 += Kernel_TcpIpRecvIPV6;
                    kernelSession.Source.Kernel.UdpIpRecv += Kernel_UdpIpRecv;
                    kernelSession.Source.Kernel.UdpIpRecvIPV6 += Kernel_UdpIpRecvIPV6;

                    kernelSession.Source.Kernel.TcpIpSend += Kernel_TcpIpSend;
                    kernelSession.Source.Kernel.TcpIpSendIPV6 += Kernel_TcpIpSendIPV6;
                    kernelSession.Source.Kernel.UdpIpSend += Kernel_UdpIpSend;
                    kernelSession.Source.Kernel.UdpIpSendIPV6 += Kernel_UdpIpSendIPV6;

                    kernelSession.Source.Process();
                }
            });
        }

        //upload events
        private void Kernel_UdpIpSendIPV6(UpdIpV6TraceData obj)
        {
            SendProcessIPV6(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_UdpIpSend(UdpIpTraceData obj)
        {
            SendProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpSendIPV6(TcpIpV6SendTraceData obj)
        {
            SendProcessIPV6(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpSend(TcpIpSendTraceData obj)
        {
            SendProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        //download events    
        private void Kernel_UdpIpRecv(UdpIpTraceData obj)
        {
            RecvProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_UdpIpRecvIPV6(UpdIpV6TraceData obj)
        {
            RecvProcessIPV6(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpRecv(TcpIpTraceData obj)
        {
            RecvProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpRecvIPV6(TcpIpV6TraceData obj)
        {
            RecvProcessIPV6(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        //update db

        //calculate the Bytes sent and recieved
        private void RecvProcess(IPAddress src, IPAddress dest, int size, string name)
        {
            bool ipCompSrc = ByteArray.Compare(src.GetAddressBytes(), localIPv4);
            bool ipCompDest = ByteArray.Compare(dest.GetAddressBytes(), localIPv4);
            if (ipCompSrc ^ ipCompDest)
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? (ipCompSrc ? !IsIPv4IPv6Private(dest) : !IsIPv4IPv6Private(src)) : //public
                    Properties.Settings.Default.NetworkType == 0 ? (ipCompSrc ?  IsIPv4IPv6Private(dest) :  IsIPv4IPv6Private(src)) : false) //private
                {
                    CurrentSessionDownloadData += (ulong)size;
                    using (ApplicationDB dB = new ApplicationDB(adapterName))
                    {
                        if (dB.CreateTable() < 0)
                            Debug.WriteLine("Error: Create table");
                        else
                        {
                            dB.InsertUniqueRow_ProcessTable(name);

                            long dateID = dB.GetID_DateTable(DateTime.Today);
                            long processID = dB.GetID_ProcessTable(name);

                            if (dB.InsertUniqueRow_ProcessDateTable(processID, dateID, size, 0) < 1)
                            {
                                dB.UpdateRow_ProcessDateTable(processID, dateID, size, 0);
                            }
                        }
                    }
                }
            }
        }

        private void RecvProcessIPV6(IPAddress src, IPAddress dest, int size, string name)
        {
            bool ipCompSrc = ByteArray.Compare(src.GetAddressBytes(), localIPv6);
            bool ipCompDest = ByteArray.Compare(dest.GetAddressBytes(), localIPv6);
            if (ipCompSrc ^ ipCompDest)
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? (ipCompSrc ? !IsIPv4IPv6Private(dest) : !IsIPv4IPv6Private(src)) : //public
                    Properties.Settings.Default.NetworkType == 0 ? (ipCompSrc ?  IsIPv4IPv6Private(dest) :  IsIPv4IPv6Private(src)) : false) //private
                {
                    CurrentSessionDownloadData += (ulong)size;
                    using (ApplicationDB dB = new ApplicationDB(adapterName))
                    {
                        if (dB.CreateTable() < 0)
                            Debug.WriteLine("Error: Create table");
                        else
                        {
                            dB.InsertUniqueRow_ProcessTable(name);

                            long dateID = dB.GetID_DateTable(DateTime.Today);
                            long processID = dB.GetID_ProcessTable(name);

                            if (dB.InsertUniqueRow_ProcessDateTable(processID, dateID, size, 0) < 1)
                            {
                                dB.UpdateRow_ProcessDateTable(processID, dateID, size, 0);
                            }
                        }
                    }
                }
            }
        }

        private void SendProcess(IPAddress src, IPAddress dest, int size, string name)
        {
            bool ipCompSrc = ByteArray.Compare(src.GetAddressBytes(), localIPv4);
            bool ipCompDest = ByteArray.Compare(dest.GetAddressBytes(), localIPv4);
            if (ipCompSrc ^ ipCompDest)
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? (ipCompSrc ? !IsIPv4IPv6Private(dest) : !IsIPv4IPv6Private(src)) : //public
                    Properties.Settings.Default.NetworkType == 0 ? (ipCompSrc ?  IsIPv4IPv6Private(dest) :  IsIPv4IPv6Private(src)) : false) //private
                {
                    CurrentSessionUploadData += (ulong)size;
                    using (ApplicationDB dB = new ApplicationDB(adapterName))
                    {
                        if (dB.CreateTable() < 0)
                            Debug.WriteLine("Error: Create table");
                        else
                        {
                            dB.InsertUniqueRow_ProcessTable(name);

                            long dateID = dB.GetID_DateTable(DateTime.Today);
                            long processID = dB.GetID_ProcessTable(name);

                            if (dB.InsertUniqueRow_ProcessDateTable(processID, dateID, 0, size) < 1)
                            {
                                dB.UpdateRow_ProcessDateTable(processID, dateID, 0, size);
                            }
                        }
                    }
                }
            }
        }
        private void SendProcessIPV6(IPAddress src, IPAddress dest, int size, string name)
        {
            bool ipCompSrc = ByteArray.Compare(src.GetAddressBytes(), localIPv6);
            bool ipCompDest = ByteArray.Compare(dest.GetAddressBytes(), localIPv6);
            if (ipCompSrc ^ ipCompDest)
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? (ipCompSrc ? !IsIPv4IPv6Private(dest) : !IsIPv4IPv6Private(src)) : //public
                    Properties.Settings.Default.NetworkType == 0 ? (ipCompSrc ?  IsIPv4IPv6Private(dest) :  IsIPv4IPv6Private(src)) : false) //private
                {
                    CurrentSessionUploadData += (ulong)size;
                    using (ApplicationDB dB = new ApplicationDB(adapterName))
                    {
                        if (dB.CreateTable() < 0)
                            Debug.WriteLine("Error: Create table");
                        else
                        {
                            dB.InsertUniqueRow_ProcessTable(name);

                            long dateID = dB.GetID_DateTable(DateTime.Today);
                            long processID = dB.GetID_ProcessTable(name);

                            if (dB.InsertUniqueRow_ProcessDateTable(processID, dateID, 0, size) < 1)
                            {
                                dB.UpdateRow_ProcessDateTable(processID, dateID, 0, size);
                            }
                        }
                    }
                }
            }
        }

        private bool IsIPv4IPv6Private(IPAddress ip)
        {
            // Map back to IPv4 if mapped to IPv6, for example "::ffff:1.2.3.4" to "1.2.3.4".
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            // Checks loopback ranges for both IPv4 and IPv6.
            if (IPAddress.IsLoopback(ip)) return true;

            // IPv4
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return IsIPv4Private(ip.GetAddressBytes());

            // IPv6
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ip.IsIPv6LinkLocal ||
#if NET6_0
                        ip.IsIPv6UniqueLocal ||
#endif
                        ip.IsIPv6SiteLocal;
            }

            throw new NotSupportedException(
                    $"IP address family {ip.AddressFamily} is not supported, expected only IPv4 (InterNetwork) or IPv6 (InterNetworkV6).");
        }

        private bool IsIPv4Private(byte[] ipv4Bytes)
        {
            // Link local (no IP assigned by DHCP): 169.254.0.0 to 169.254.255.255 (169.254.0.0/16)
            bool IsLinkLocal() => ipv4Bytes[0] == 169 && ipv4Bytes[1] == 254;

            // Class A private range: 10.0.0.0 – 10.255.255.255 (10.0.0.0/8)
            bool IsClassA() => ipv4Bytes[0] == 10;

            // Class B private range: 172.16.0.0 – 172.31.255.255 (172.16.0.0/12)
            bool IsClassB() => ipv4Bytes[0] == 172 && ipv4Bytes[1] >= 16 && ipv4Bytes[1] <= 31;

            // Class C private range: 192.168.0.0 – 192.168.255.255 (192.168.0.0/16)
            bool IsClassC() => ipv4Bytes[0] == 192 && ipv4Bytes[1] == 168;

            return IsLinkLocal() || IsClassA() || IsClassC() || IsClassB();
        }

        //------property changers---------------//

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
            if(kernelSession != null)
            {
                kernelSession.Dispose();
            }
        }
    }
}
