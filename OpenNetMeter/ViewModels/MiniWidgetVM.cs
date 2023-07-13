using OpenNetMeter.Utilities;
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace OpenNetMeter.ViewModels
{
    public class MiniWidgetVM : INotifyPropertyChanged

    {

        public string? nepaliDate;
        public string? NepaliDate
        {
            get { return nepaliDate; }
            set
            {
                nepaliDate = value;
                OnPropertyChanged("NepaliDate");
            }
        }

        private long currentSessionDownloadData;
        public long CurrentSessionDownloadData
        {
            get { return currentSessionDownloadData; }
            set
            {
                currentSessionDownloadData = value;
                OnPropertyChanged("CurrentSessionDownloadData");
            }
        }
        private long currentSessionUploadData;
        public long CurrentSessionUploadData
        {
            get { return currentSessionUploadData; }
            set
            {
                currentSessionUploadData = value;
                OnPropertyChanged("CurrentSessionUploadData");
            }
        }

        public long downloadSpeed;
        public long DownloadSpeed
        {
            get { return downloadSpeed; }
            set
            {
                downloadSpeed = value;
                OnPropertyChanged("DownloadSpeed");
            }
        }
        public long uploadSpeed;
        public long UploadSpeed
        {
            get { return uploadSpeed; }
            set
            {
                uploadSpeed = value;
                OnPropertyChanged("UploadSpeed");
            }
        }

        private double width;
        public double Width
        {
            get { return width; }
            set
            {
                width = value;
                OnPropertyChanged("Width");
            }
        }

        private double height;
        public double Height
        {
            get { return height; }
            set
            {
                height = value;
                OnPropertyChanged("Height");
            }
        }

        public async void GetPokemon()
        {
            //Define your baseUrl
            string baseUrl = "https://results.bimal1412.com.np/nepalidatev2/";
            string mydat = "";
            //Have your using statements within a try/catch block
            try
            {
                //We will now define your HttpClient with your first using statement which will use a IDisposable.
                using (HttpClient client = new HttpClient())
                {
                    //In the next using statement you will initiate the Get Request, use the await keyword so it will execute the using statement in order.
                    using (HttpResponseMessage res = await client.GetAsync(baseUrl))
                    {
                        //Then get the content from the response in the next using statement, then within it you will get the data, and convert it to a c# object.
                        using (HttpContent content = res.Content)
                        {
                            //Now assign your content to your data variable, by converting into a string using the await keyword.
                            var data = await content.ReadAsStringAsync();
                            //If the data isn't null return log convert the data using newtonsoft JObject Parse class method on the data.
                            if (data != null)
                            {
                                //Now log your data in the console
                                Console.WriteLine("data------------{0}", data);
                                var subs = data.Substring(6).Split(" ");
                                var datestring = $"{subs[2]} {subs[3]}, {subs[1]}";
                                //NepaliDate = data.Substring(6);
                                NepaliDate = datestring;
                                                           }
                            else
                            {
                                Console.WriteLine("NO Data----------");
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Exception Hit------------");
                Console.WriteLine(exception);
            }
        }

        public MiniWidgetVM()
        {
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;
            DownloadSpeed = 0;
            UploadSpeed = 0;

            //NepaliDate = "2080/12/11";
            GetPokemon();
            
            Size size1 = UIMeasure.Shape(new TextBlock { Text = "jay nepalS :", FontSize = 12, Padding = new Thickness(0) });
            Size size2 = UIMeasure.Shape(new TextBlock { Text = "1024.00Mbps", FontSize = 12, Padding = new Thickness(5,0,0,0) });
            int widthMargins = 5 + 5; //these are from the miniwidget xaml margins
            Width = size1.Width + size2.Width + widthMargins;
            int heightMargins = 2 + 2; //these are from the miniwidget xaml margins
            Height = size1.Height * 2 + heightMargins * 2;
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
