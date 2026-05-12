using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;

namespace Barcode_File_Find
{
    public class IllustratorAutomationService
    {
        private static readonly string[] FileMenuNames = ["파일", "File"];
        private static readonly string[] CuttingMasterMenuNames = ["Cutting Master5", "Cutting Master 5"];
        private static readonly string[] SendMenuNames =
        [
            "Cutting Master5로 보내기",
            "Cutting Master5 로 보내기",
            "Cutting Master5 보내기",
            "Cutting Master 5로 보내기",
            "Cutting Master 5 로 보내기",
            "Cutting Master 5 보내기",
            "Send to Cutting Master5"
        ];

        public async Task<bool> SendToCuttingMasterAsync(
            string filePath,
            TimeSpan timeout,
            TimeSpan documentSettleDelay,
            Action<string>? statusCallback = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName = Path.GetFileName(filePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

            statusCallback?.Invoke("Illustrator 실행을 확인하는 중입니다.");
            Process? illustrator = await WaitForIllustratorAsync(timeout, cancellationToken);
            if (illustrator == null)
            {
                Logger.Log($"Illustrator 실행 확인 실패: 제한 시간({timeout.TotalSeconds:0}초)을 초과했습니다.");
                return false;
            }

            BringToFront(illustrator);

            statusCallback?.Invoke("Illustrator 문서가 열릴 때까지 기다리는 중입니다.");
            bool documentOpened = await WaitForDocumentAsync(illustrator, fileName, fileNameWithoutExtension, timeout, cancellationToken);
            if (!documentOpened)
            {
                Logger.Log($"Illustrator 문서 열림 확인 실패: '{fileName}' 문서 제목을 찾지 못했습니다.");
                return false;
            }

            statusCallback?.Invoke($"Illustrator 문서 로딩이 안정될 때까지 {documentSettleDelay.TotalSeconds:0}초 기다리는 중입니다.");
            await Task.Delay(documentSettleDelay, cancellationToken);
            BringToFront(illustrator);

            statusCallback?.Invoke("Cutting Master5 메뉴를 실행하는 중입니다.");
            bool menuExecuted = await InvokeCuttingMasterMenuAsync(illustrator, timeout, cancellationToken);
            if (!menuExecuted)
            {
                Logger.Log("Cutting Master5 메뉴 실행 실패: 메뉴 항목을 찾거나 실행하지 못했습니다.");
            }

            return menuExecuted;
        }

        private static async Task<Process?> WaitForIllustratorAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            DateTime deadline = DateTime.Now.Add(timeout);

            while (DateTime.Now < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Process? process = Process.GetProcessesByName("Illustrator")
                    .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);

                if (process != null)
                {
                    return process;
                }

                await Task.Delay(500, cancellationToken);
            }

            return null;
        }

        private static async Task<bool> WaitForDocumentAsync(
            Process illustrator,
            string fileName,
            string fileNameWithoutExtension,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            DateTime deadline = DateTime.Now.Add(timeout);

            while (DateTime.Now < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                illustrator.Refresh();
                string title = illustrator.MainWindowTitle;

                if (title.Contains(fileName, StringComparison.OrdinalIgnoreCase) ||
                    title.Contains(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                await Task.Delay(500, cancellationToken);
            }

            return false;
        }

        private static async Task<bool> InvokeCuttingMasterMenuAsync(
            Process illustrator,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            DateTime deadline = DateTime.Now.Add(timeout);
            int attempt = 1;

            BringToFront(illustrator);
            AutomationElement app = AutomationElement.FromHandle(illustrator.MainWindowHandle);
            if (app == null)
            {
                Logger.Log("Illustrator UIAutomation 연결 실패: MainWindowHandle에서 AutomationElement를 만들지 못했습니다.");
                return false;
            }

            while (DateTime.Now < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BringToFront(illustrator);

                Logger.Log($"CM5 메뉴 실행 시도 {attempt}회: 파일 메뉴 열기 시작");
                OpenFileMenu(illustrator, app, attempt);
                await Task.Delay(1000, cancellationToken);

                AutomationElement root = AutomationElement.RootElement;
                AutomationElement? cuttingMaster = FindMenuItem(root, CuttingMasterMenuNames);
                if (cuttingMaster == null)
                {
                    Logger.Log("Cutting Master5 메뉴 탐색 실패: 열린 메뉴에서 Cutting Master5 항목을 찾지 못했습니다.");
                    attempt++;
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                DumpElementInfo(cuttingMaster, "Cutting Master5 메뉴 항목");
                if (!TryExpandMenuItem(cuttingMaster))
                {
                    Logger.Log($"Cutting Master5 메뉴 확장 실패: '{GetElementName(cuttingMaster)}'");
                    attempt++;
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                await Task.Delay(800, cancellationToken);

                root = AutomationElement.RootElement;
                AutomationElement? sendMenu = FindMenuItem(root, SendMenuNames);
                if (sendMenu == null)
                {
                    Logger.Log("Cutting Master5 하위 메뉴 탐색 실패: '보내기' 메뉴 항목을 찾지 못했습니다.");
                    attempt++;
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                DumpElementInfo(sendMenu, "Cutting Master5 보내기 메뉴 항목");
                if (TryRunMenuItem(sendMenu))
                {
                    return true;
                }

                Logger.Log($"Cutting Master5 하위 메뉴 실행 실패: '{GetElementName(sendMenu)}'");
                attempt++;
                await Task.Delay(500, cancellationToken);
            }

            return false;
        }
        private static void OpenFileMenu(Process illustrator, AutomationElement app, int attempt)
        {
            AutomationElement? fileMenu = FindMenuItem(app, FileMenuNames);
            if (fileMenu != null)
            {
                DumpElementInfo(fileMenu, "파일 메뉴 항목");
                if (TryExpandMenuItem(fileMenu))
                {
                    Logger.Log("파일 메뉴 열기 성공: UIAutomation 방식");
                    return;
                }

                Logger.Log("파일 메뉴를 찾았지만 UIAutomation 확장에 실패했습니다. Alt+F를 시도합니다.");
            }
            else
            {
                Logger.Log("Illustrator 창 내부에서 파일 메뉴 항목을 찾지 못했습니다. Alt+F를 시도합니다.");
            }

            BringToFront(illustrator);
            SendKeys.SendWait("%f");
            Thread.Sleep(700);

            if (attempt % 2 == 0)
            {
                TryClickFileMenuByPosition(illustrator);
            }
        }

        private static void TryClickFileMenuByPosition(Process illustrator)
        {
            try
            {
                if (!GetWindowRect(illustrator.MainWindowHandle, out RECT rect))
                {
                    Logger.Log("파일 메뉴 좌표 클릭 실패: Illustrator 창 위치를 확인하지 못했습니다.");
                    return;
                }

                int x = rect.Left + 25;
                int y = rect.Top + 35;
                Logger.Log($"파일 메뉴 좌표 클릭 시도: X={x}, Y={y}");
                SetCursorPos(x, y);
                mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, UIntPtr.Zero);
                Thread.Sleep(700);
            }
            catch (Exception ex) when (ex is InvalidOperationException or COMException)
            {
                Logger.Log($"파일 메뉴 좌표 클릭 실패: {ex.Message}");
            }
        }
        private static bool TryExpandMenuItem(AutomationElement element)
        {
            return TryExpandOrInvoke(element, expandOnly: true) ||
                   TryClickElementCenter(element);
        }

        private static bool TryRunMenuItem(AutomationElement element)
        {
            return TryExpandOrInvoke(element, expandOnly: false) ||
                   TryClickElementCenter(element);
        }

        private static bool TryClickElementCenter(AutomationElement element)
        {
            try
            {
                System.Windows.Rect bounds = element.Current.BoundingRectangle;
                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                {
                    return false;
                }

                int x = (int)(bounds.Left + bounds.Width / 2);
                int y = (int)(bounds.Top + bounds.Height / 2);
                Logger.Log($"메뉴 항목 중앙 클릭 시도: '{GetElementName(element)}', X={x}, Y={y}");
                SetCursorPos(x, y);
                mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, UIntPtr.Zero);
                return true;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (COMException)
            {
                return false;
            }
        }

        private static AutomationElement? FindMenuItem(AutomationElement root, string[] names)
        {
            var menuItemCondition = new PropertyCondition(
                AutomationElement.ControlTypeProperty,
                ControlType.MenuItem);

            foreach (string name in names)
            {
                var condition = new AndCondition(
                    menuItemCondition,
                    new PropertyCondition(AutomationElement.NameProperty, name));

                AutomationElement? item = root.FindFirst(TreeScope.Descendants, condition);
                if (item != null)
                {
                    return item;
                }
            }

            AutomationElementCollection menuItems = root.FindAll(TreeScope.Descendants, menuItemCondition);
            foreach (AutomationElement item in menuItems)
            {
                string itemName = GetElementName(item);
                string normalizedItemName = NormalizeMenuName(itemName);

                if (names.Any(name =>
                    itemName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                    normalizedItemName.Contains(NormalizeMenuName(name), StringComparison.OrdinalIgnoreCase)))
                {
                    return item;
                }
            }

            return null;
        }

        private static void DumpElementInfo(AutomationElement element, string label)
        {
            try
            {
                string supportedPatterns = string.Join(", ", element.GetSupportedPatterns().Select(p => p.ProgrammaticName));
                System.Windows.Rect bounds = element.Current.BoundingRectangle;
                Logger.Log($"{label}: Name='{GetElementName(element)}', Bounds={bounds}, Patterns={supportedPatterns}");
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException)
            {
                Logger.Log($"{label} 정보 확인 실패: {ex.Message}");
            }
        }

        private static string GetElementName(AutomationElement element)
        {
            try
            {
                return element.Current.Name ?? string.Empty;
            }
            catch (ElementNotAvailableException)
            {
                return string.Empty;
            }
        }

        private static string NormalizeMenuName(string name)
        {
            return new string(name.Where(c => !char.IsWhiteSpace(c)).ToArray());
        }

        private static bool TryExpandOrInvoke(AutomationElement element, bool expandOnly)
        {
            try
            {
                if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object expandPattern))
                {
                    ((ExpandCollapsePattern)expandPattern).Expand();
                    return true;
                }

                if (!expandOnly && element.TryGetCurrentPattern(InvokePattern.Pattern, out object invokePattern))
                {
                    ((InvokePattern)invokePattern).Invoke();
                    return true;
                }
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (COMException)
            {
                return false;
            }

            return false;
        }

        private static void BringToFront(Process process)
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                ShowWindow(process.MainWindowHandle, SW_RESTORE);
                SetForegroundWindow(process.MainWindowHandle);
            }
        }

        private const int SW_RESTORE = 9;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);
    }
}