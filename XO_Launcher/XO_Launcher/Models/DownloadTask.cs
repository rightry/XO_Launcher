using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace XO_Launcher.Models
{
    /// <summary>
    /// 单个下载任务。进度状态变化通过 INotifyPropertyChanged 通知消息中心的绑定界面。
    /// 所有属性必须在 UI 线程上更新（由 DownloadService 负责调度）。
    /// </summary>
    public sealed class DownloadTask : INotifyPropertyChanged
    {
        private readonly CancellationTokenSource _cts = new();
        private double _progress;
        private string _status = "等待下载";
        private string _fileName;
        private string _detail = "";
        private long _downloadedBytes;
        private long _totalBytes;
        private string? _progressHint;

        public DownloadTask(string title, string fileName)
        {
            Title = title;
            _fileName = fileName;
        }

        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Title { get; }
        public string? ProjectType { get; set; }
        public CancellationToken Token => _cts.Token;

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); }
        }

        public string ProgressText => $"{Progress:0}%";

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressText));
                OnPropertyChanged(nameof(IsIndeterminate));
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(CanCancel));
            }
        }

        /// <summary>附加信息：完成后的文件路径 / 失败原因等。</summary>
        public string Detail
        {
            get => _detail;
            set { _detail = value; OnPropertyChanged(); }
        }

        public long DownloadedBytes
        {
            get => _downloadedBytes;
            set { _downloadedBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransferText)); }
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set { _totalBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransferText)); OnPropertyChanged(nameof(IsIndeterminate)); }
        }

        public string TransferText => string.IsNullOrEmpty(ProgressHint)
            ? $"{FormatHelper.FormatBytes(DownloadedBytes)} / {FormatHelper.FormatBytes(TotalBytes)}"
            : ProgressHint;

        public bool IsIndeterminate => TotalBytes <= 0 && Status == "下载中" && string.IsNullOrEmpty(_progressHint);
        public bool IsActive => Status is "下载中" or "等待下载" or "正在取消";
        public bool CanCancel => Status is "下载中" or "等待下载";

        /// <summary>安装类任务用「12 / 40 个文件」替代字节显示。</summary>
        public string? ProgressHint
        {
            get => _progressHint;
            set { _progressHint = value; OnPropertyChanged(); OnPropertyChanged(nameof(TransferText)); OnPropertyChanged(nameof(IsIndeterminate)); }
        }

        public void RequestCancel()
        {
            try { _cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
