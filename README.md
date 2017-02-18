# DownloadManager

Lightweight download manager that can be embedded in most .Net projects. It was created with the purpose of being used in Xamarin Apps to handle multiple downloads in an orderly fashion.


To run DownloadManager needs an implementation of IStorage to know where it can get output streams from. This is due to System.IO.File not being available in PCL.

## HttpService
HttpService has a constructor with an optional HttpClient so that it is possible to use ModernHttpClient on Xamarin.iOS and Xamarin.Android

## DownloadManager
The workhorse of the project. 


##IDownloadManager
```C#
int NumberOfConcurrentDownloads { get; set; }
```
Sets the max number of downloads to run concurrently. You will most likely need to increase the DefaultConnectionLimit of ServicePointManager since it defaults to 2 to get any effect (`ServicePointManager.DefaultConnectionLimit = 10`)

```C#
int MaxRetryCount { get; set; }
```
The manager will try do to download any failed download multiple times before calling it quits. Defaults to 3
```C#
IDownload DownloadFile(string url, string assetFilename  = null);
```
Qeues a file and returns a `IDownload` object, if `assetfilename` is not entered it will create a file with guid as filename that can be read in the `IDownload` object
```C#
void CancelDownload(string url);
void CancelDownload(IDownload download);
```
Cancels the download, if it is in progress it will be aborted. 

```C#
IObservable<int> DownloadProgress { get; }
```
An observable with count of the current queue downloads and the current downloads in progress
```C#
IObservable<IDownload> DownloadUpdated { get; }
```
Will be called when a download changes status

##IDownload
```C#
IObservable<double> Progress { get; }
```
Updates when the download is in progress, value from 0.0 to 1.0.
```C#
string Url { get; }
```
The url for the source of the download
```C#
string FileIdentifier { get; }
```
The filename entered or a random guid
```C#
DownloadStatus Status { get; }
```
The current status of the download
```C#
int ErrorCount { get; }
```
The numbers of failed attempts to download the file
