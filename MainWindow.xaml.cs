using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Barcode_File_Find
{
    public partial class MainWindow : Window
    {
        private const string IllustratorExtension = ".eps";
        private static readonly TimeSpan BarcodeInputIdleDelay = TimeSpan.FromMilliseconds(800);
        private AppSettings _settings;
        private readonly IllustratorAutomationService _illustratorAutomationService = new();
        private readonly DispatcherTimer _barcodeInputTimer;
        private bool _isLoadingSettings;
        private CancellationTokenSource? _currentOperationCts;

        public MainWindow()
        {
            InitializeComponent();
            _barcodeInputTimer = new DispatcherTimer
            {
                Interval = BarcodeInputIdleDelay
            };
            _barcodeInputTimer.Tick += BarcodeInputTimer_Tick;
            _settings = SettingsManager.Load();
            NormalizeSettings();
            ApplySettingsToUi();
            Logger.OnLogAdded += Logger_OnLogAdded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BarcodeTextBox.Focus();
        }

        private void Logger_OnLogAdded(string logMessage)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText(logMessage + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            });
        }

        private void BrowseButton1_Click(object sender, RoutedEventArgs e)
        {
            BrowseFolder(1);
        }

        private void BrowseButton2_Click(object sender, RoutedEventArgs e)
        {
            BrowseFolder(2);
        }

        private void BrowseFolder(int folderNumber)
        {
            var dialog = new OpenFolderDialog
            {
                Title = $"검색 폴더 {folderNumber} 선택"
            };

            if (dialog.ShowDialog() == true)
            {
                if (folderNumber == 1)
                {
                    FolderPathTextBox1.Text = dialog.FolderName;
                    _settings.SearchDirectory = dialog.FolderName;
                }
                else
                {
                    FolderPathTextBox2.Text = dialog.FolderName;
                    _settings.SearchDirectory2 = dialog.FolderName;
                }

                SettingsManager.Save(_settings);
                SetStatus("설정이 저장되었습니다.", false);
                BarcodeTextBox.Focus();
            }
        }

        private async void BarcodeTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _barcodeInputTimer.Stop();
                await ProcessBarcodeInputAsync();
                e.Handled = true;
            }
        }

        private void BarcodeTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_currentOperationCts != null || !BarcodeTextBox.IsEnabled)
            {
                _barcodeInputTimer.Stop();
                return;
            }

            if (string.IsNullOrWhiteSpace(BarcodeTextBox.Text))
            {
                _barcodeInputTimer.Stop();
                return;
            }

            _barcodeInputTimer.Stop();
            _barcodeInputTimer.Start();
        }

        private async void BarcodeInputTimer_Tick(object? sender, EventArgs e)
        {
            _barcodeInputTimer.Stop();
            await ProcessBarcodeInputAsync();
        }

        private async Task ProcessBarcodeInputAsync()
        {
            _barcodeInputTimer.Stop();

            if (_currentOperationCts != null)
            {
                SetStatus("이미 작업이 진행 중입니다. 중지 후 다시 시도해 주세요.", true);
                return;
            }

            string barcode = BarcodeTextBox.Text.Trim();
            BarcodeTextBox.Clear();

            if (!string.IsNullOrEmpty(barcode))
            {
                _currentOperationCts = new CancellationTokenSource();
                SetOperationInProgress(true);

                try
                {
                    await ProcessBarcodeAsync(barcode, _currentOperationCts.Token);
                }
                catch (OperationCanceledException)
                {
                    SetStatus("작업이 중지되었습니다.", false);
                    Logger.Log($"스캔된 '{barcode}': 사용자가 작업을 중지했습니다.");
                }
                finally
                {
                    _currentOperationCts.Dispose();
                    _currentOperationCts = null;
                    SetOperationInProgress(false);
                }
            }

            BarcodeTextBox.Focus();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _currentOperationCts?.Cancel();
            SetStatus("작업 중지를 요청했습니다.", false);
        }

        private async Task ProcessBarcodeAsync(string barcode, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var searchDirs = new[] { _settings.SearchDirectory, _settings.SearchDirectory2 }
                .Where(dir => !string.IsNullOrWhiteSpace(dir))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var validSearchDirs = searchDirs
                .Where(Directory.Exists)
                .ToArray();

            if (validSearchDirs.Length == 0)
            {
                SetStatus("올바른 검색 폴더가 없습니다.", true);
                Logger.Log($"'{barcode}' 검색 실패: 올바른 검색 폴더가 없습니다.");
                return;
            }

            foreach (var invalidDir in searchDirs.Except(validSearchDirs, StringComparer.OrdinalIgnoreCase))
            {
                Logger.Log($"검색에서 제외된 폴더: {invalidDir}");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var enumOptions = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                    MatchType = MatchType.Simple
                };
                var allFiles = validSearchDirs
                    .SelectMany(searchDir => Directory.GetFiles(searchDir, $"{barcode}{IllustratorExtension}", enumOptions))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                var files = allFiles.Where(f =>
                {
                    string expectedFileName = $"{barcode}{IllustratorExtension}";
                    return Path.GetFileName(f).Equals(expectedFileName, StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();

                var duplicateFileNameExists = files
                    .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .Any(group => group.Count() > 1);

                if (files.Length == 0)
                {
                    SetStatus($"바코드에 해당하는 파일을 찾을 수 없습니다: {barcode}", true);
                    Logger.Log($"스캔된 '{barcode}': 파일을 찾을 수 없습니다.");
                }
                else if (files.Length == 1 && !duplicateFileNameExists)
                {
                    await ExecuteFileAsync(files[0], barcode, cancellationToken);
                }
                else
                {
                    Logger.Log($"스캔된 '{barcode}': 여러 파일이 발견되었습니다. ({files.Length}개) 사용자의 선택을 기다립니다.");
                    var selectionWindow = new FileSelectionWindow(files)
                    {
                        Owner = this
                    };

                    if (selectionWindow.ShowDialog() == true && !string.IsNullOrEmpty(selectionWindow.SelectedFilePath))
                    {
                        await ExecuteFileAsync(selectionWindow.SelectedFilePath, barcode, cancellationToken);
                    }
                    else
                    {
                        SetStatus("파일 선택이 취소되었습니다.", false);
                        Logger.Log($"사용자가 '{barcode}'에 대한 파일 선택을 취소했습니다.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetStatus($"파일 검색 중 오류 발생: {ex.Message}", true);
                Logger.Log($"'{barcode}' 검색 중 오류 발생: {ex.Message}");
            }
            finally
            {
                BarcodeTextBox.Focus();
            }
        }

        private async Task ExecuteFileAsync(string filePath, string barcode, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Path.GetExtension(filePath).Equals(IllustratorExtension, StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus($"지원하지 않는 파일 형식입니다. {IllustratorExtension} 파일만 실행할 수 있습니다.", true);
                    Logger.Log($"스캔된 '{barcode}': '{filePath}' 실행 취소. {IllustratorExtension} 파일이 아닙니다.");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
                SetStatus($"Illustrator에서 여는 중: {Path.GetFileName(filePath)}", false);
                Logger.Log($"스캔된 '{barcode}': '{filePath}' 실행");

                if (!_settings.RunCuttingMasterAfterOpen)
                {
                    SetStatus($"Illustrator에서 파일을 열었습니다: {Path.GetFileName(filePath)}", false);
                    Logger.Log($"스캔된 '{barcode}': CM5 실행 안 함 설정으로 파일 열기까지만 완료");
                    return;
                }

                if (!_settings.AutomationMode.Equals("UIAutomation", StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus($"지원하지 않는 자동화 방식입니다: {_settings.AutomationMode}", true);
                    Logger.Log($"스캔된 '{barcode}': 지원하지 않는 자동화 방식 '{_settings.AutomationMode}'");
                    return;
                }

                bool sent = await _illustratorAutomationService.SendToCuttingMasterAsync(
                    filePath,
                    TimeSpan.FromSeconds(_settings.IllustratorStartupTimeoutSeconds),
                    TimeSpan.FromSeconds(_settings.IllustratorDocumentSettleSeconds),
                    message => Dispatcher.Invoke(() => SetStatus(message, false)),
                    cancellationToken);

                if (sent)
                {
                    SetStatus("Cutting Master5로 보내기 메뉴를 실행했습니다.", false);
                    Logger.Log($"스캔된 '{barcode}': Cutting Master5로 보내기 메뉴 실행 완료");
                }
                else
                {
                    SetStatus("Cutting Master5 메뉴 실행에 실패했습니다. 로그를 확인해 주세요.", true);
                    Logger.Log($"스캔된 '{barcode}': Cutting Master5로 보내기 메뉴 실행 실패");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetStatus($"파일 실행 실패: {ex.Message}", true);
                Logger.Log($"스캔된 '{barcode}': '{filePath}' 실행 실패. 오류: {ex.Message}");
            }
        }

        private void IllustratorTimeoutTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveAutomationSettingsFromUi();
        }

        private void NormalizeSettings()
        {
            if (_settings.IllustratorStartupTimeoutSeconds <= 0)
            {
                _settings.IllustratorStartupTimeoutSeconds = 60;
            }

            if (_settings.IllustratorDocumentSettleSeconds <= 0)
            {
                _settings.IllustratorDocumentSettleSeconds = 3;
            }

            _settings.AutomationMode = "UIAutomation";
            SettingsManager.Save(_settings);
        }

        private void ApplySettingsToUi()
        {
            _isLoadingSettings = true;
            FolderPathTextBox1.Text = _settings.SearchDirectory;
            FolderPathTextBox2.Text = _settings.SearchDirectory2;
            IllustratorTimeoutTextBox.Text = _settings.IllustratorStartupTimeoutSeconds.ToString();
            IllustratorSettleDelayTextBox.Text = _settings.IllustratorDocumentSettleSeconds.ToString();
            RunCuttingMasterCheckBox.IsChecked = _settings.RunCuttingMasterAfterOpen;
            _isLoadingSettings = false;
        }

        private void SaveAutomationSettingsFromUi()
        {
            if (_isLoadingSettings)
            {
                return;
            }

            if (!int.TryParse(IllustratorTimeoutTextBox.Text.Trim(), out int timeoutSeconds) || timeoutSeconds <= 0)
            {
                timeoutSeconds = 60;
                IllustratorTimeoutTextBox.Text = timeoutSeconds.ToString();
            }

            if (!int.TryParse(IllustratorSettleDelayTextBox.Text.Trim(), out int settleSeconds) || settleSeconds <= 0)
            {
                settleSeconds = 3;
                IllustratorSettleDelayTextBox.Text = settleSeconds.ToString();
            }

            _settings.IllustratorStartupTimeoutSeconds = timeoutSeconds;
            _settings.IllustratorDocumentSettleSeconds = settleSeconds;
            _settings.RunCuttingMasterAfterOpen = RunCuttingMasterCheckBox.IsChecked == true;
            _settings.AutomationMode = "UIAutomation";
            SettingsManager.Save(_settings);
            SetStatus("자동화 설정이 저장되었습니다.", false);
        }

        private void RunCuttingMasterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            _settings.RunCuttingMasterAfterOpen = RunCuttingMasterCheckBox.IsChecked == true;
            SettingsManager.Save(_settings);
            SetStatus("자동화 설정이 저장되었습니다.", false);
            BarcodeTextBox.Focus();
        }

        private void SetOperationInProgress(bool isInProgress)
        {
            StopButton.IsEnabled = isInProgress;
            BarcodeTextBox.IsEnabled = !isInProgress;
        }

        private void SetStatus(string message, bool isError)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = isError ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Blue;
        }
    }
}

