# 바코드 파일 찾기

QR/바코드 스캐너로 읽은 파일명을 기준으로 `.eps` 파일을 찾아 Adobe Illustrator에서 여는 Windows WPF 프로그램입니다. 필요하면 Illustrator에서 파일을 연 뒤 Cutting Master 5 메뉴 실행까지 이어서 처리합니다.

## 주요 기능

- 바코드/QR 스캔 값으로 `.eps` 파일 검색
- 검색 폴더 2개까지 지정 가능
- 검색 결과가 1개면 바로 Illustrator로 열기
- 같은 파일명이 여러 위치에서 발견되면 선택 창 표시
- 파일 열기 후 Cutting Master 5 실행 옵션 제공
- 스캐너 입력창 포커스 상태를 형광 연두색 배경과 진한 녹색 테두리로 표시
- 프로그램 창이 활성화되어 있을 때 입력창 밖에서 `Enter`를 누르면 스캐너 입력창으로 자동 포커싱
- 입력창이 활성화된 상태에서는 `바코드 + Enter`로 즉시 조회 시작
- 작업 중지 버튼, 실행 로그, 상태 메시지 제공
- 작업표시줄/트레이 아이콘 적용

## 실행 환경

- Windows
- .NET 8 Windows Desktop Runtime 또는 self-contained publish 빌드
- Adobe Illustrator
- Cutting Master 5, 자동 실행 옵션을 사용할 경우

## 실행 파일 다운로드

최신 실행 파일은 GitHub 릴리스에서 받을 수 있습니다.

[바코드 파일 찾기 다운로드](https://github.com/rlatkdxo269-commits/Barcode_File_Find/releases/latest)

릴리스 페이지에서 `Barcode_File_Find.exe` 파일을 내려받은 뒤 실행하면 됩니다.

이 프로그램은 단일 실행 파일 형태로 배포됩니다. 별도의 설치 과정 없이 `Barcode_File_Find.exe`를 실행하면 되고, 같은 폴더에 `settings.json`과 `log.txt`가 자동으로 생성됩니다.

## 사용 방법

1. 프로그램을 실행합니다.
2. `검색 폴더 1`, `검색 폴더 2`를 지정합니다.
3. 필요에 따라 Illustrator 대기 시간과 문서 안정화 대기 시간을 조정합니다.
4. 스캐너 입력창에 포커스가 있는 상태에서 QR/바코드를 스캔합니다.
5. 스캐너가 마지막에 `Enter`를 보내면 자동으로 파일 검색이 시작됩니다.

스캐너 입력창에 포커스가 없을 때 스캐너의 `Enter`만 먼저 들어오면, 프로그램은 조회를 시작하지 않고 입력창으로 포커스를 이동합니다. 그 다음 스캔부터 `바코드 + Enter` 흐름으로 처리됩니다.

## 파일 검색 규칙

스캔 값이 `ABC123`이면 프로그램은 지정된 검색 폴더 아래에서 다음 파일을 찾습니다.

```text
ABC123.eps
```

검색은 하위 폴더까지 포함하며, 파일명은 대소문자를 구분하지 않습니다.

## 개발 및 빌드

개발용 빌드:

```powershell
dotnet build
```

단일 실행 파일 배포 빌드:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-single
```

실사용 배포본은 `publish-single/Barcode_File_Find.exe`입니다. 실행하면 실행 파일이 있는 폴더에 `settings.json`과 `log.txt`가 생성되어 설정과 로그가 유지됩니다.

## 프로젝트 구조

```text
Barcode_File_Find/
├─ Assets/                         # 앱/트레이 아이콘
├─ MainWindow.xaml                 # 메인 UI
├─ MainWindow.xaml.cs              # 스캔, 검색, 실행 흐름
├─ IllustratorAutomationService.cs # Illustrator / Cutting Master 자동화
├─ FileSelectionWindow.xaml        # 중복 파일 선택 창
├─ SettingsManager.cs              # settings.json 저장/로드
└─ Logger.cs                       # 실행 로그
```

## Git 관리 제외 항목

다음 폴더와 파일은 빌드/배포 산출물 또는 로컬 실행 데이터라서 Git에 포함하지 않습니다.

```text
bin/
obj/
publish/
publish-single/
portable/
*.user
.vs/
```
