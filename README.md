# ImageGen

NovelAI Image Generation API를 데스크톱에서 편리하게 사용할 수 있도록 만든 Windows용 WPF 클라이언트입니다. 텍스트 기반 이미지 생성부터 Img2Img, 인페인팅, 참조 이미지, 캐릭터 프롬프트, 노드 그래프 워크플로까지 하나의 UI에서 다룰 수 있습니다.

![](https://ch99.atlasimg.org/n1.PNG)

> [!IMPORTANT]
> 이 프로젝트는 NovelAI의 비공식 클라이언트입니다. NovelAI 또는 Anlatan과 제휴하거나 공식 지원을 받는 제품이 아닙니다. 이미지 생성에는 사용자의 NovelAI 액세스 토큰과 Anlas가 필요하며, 서비스 이용 약관과 생성물 관련 규정을 직접 확인해야 합니다.

## 주요 기능

### 이미지 생성

- Text2Image, Img2Img, Inpaint 생성 모드
- NovelAI Diffusion 3, 4, 4.5 및 Furry 모델 선택
- 해상도, 스텝, 프롬프트 가이던스, 샘플러, 노이즈 스케줄, SMEA 설정
- 고정 시드와 무작위 시드 지원
- 생성 중 스트리밍 미리보기
- 생성 결과 자동 저장 및 클립보드 복사
- 현재 Anlas 잔액과 최근 생성 비용 표시
- 프롬프트 태그 자동 완성

### 프롬프트와 참조 이미지

- Positive/Negative 프롬프트 분리
- 여러 캐릭터의 프롬프트, 네거티브 프롬프트, 화면 내 위치 지정
- 폴더 구조를 지원하는 캐릭터 프리셋 저장 및 재사용
- 여러 Vibe Transfer 참조 이미지와 개별 강도 설정
- Character, Character & Style, Style 타입의 Precise Reference
- 이미지를 드래그 앤 드롭하거나 `Ctrl+V`로 붙여 넣은 뒤 Img2Img, Inpaint, Vibe Transfer, Precise Reference 중 용도 선택

![](https://ch99.atlasimg.org/n2.PNG)

### 인페인팅

- 원본 이미지 위에서 직접 마스크 편집
- 브러시 크기 조절, 지우기, 실행 취소/다시 실행
- 마스크 미리보기와 초기화

![](https://ch99.atlasimg.org/n3.PNG)

### 노드 그래프

- Begin, End, Base, Character, Base Concat, Graph 노드 기반 워크플로 구성
- 노드 연결, 우회, 복제, 복사/붙여넣기 및 입력 순서 변경
- 그래프 확대/축소와 패널 접기
- 워크플로를 JSON으로 저장하고 다시 불러오기
- 저장된 그래프를 다른 그래프 안에서 재사용
- Begin 노드부터 연결된 생성 체인 실행

![](https://ch99.atlasimg.org/n4.PNG)

### Director Tools

입력 이미지에 다음 NovelAI Director 도구를 적용하고 결과를 저장하거나 클립보드로 복사할 수 있습니다.

- 배경 제거 (`bg-removal`)
- 라인아트 변환 (`lineart`)
- 스케치 변환 (`sketch`)
- 컬러라이즈 (`colorize`)
- 표정/감정 변경 (`emotion`)
- 디클러터 (`declutter`)

![](https://ch99.atlasimg.org/n5.PNG)

### EXIF Viewer

PNG, JPG, JPEG 이미지를 열거나 드래그 앤 드롭해 이미지에 포함된 메타데이터를 확인할 수 있습니다.

## 요구 사항

- Windows 10/11 x64
- NovelAI 계정, 이미지 생성 API에 사용할 액세스 토큰 및 충분한 Anlas
- 소스에서 실행할 경우 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 저장소 업데이트 기능을 사용할 경우 Git

## 시작하기

### 1. 저장소 복제

```powershell
git clone https://github.com/Yoon999/ImageGen.git
cd ImageGen
```

### 2. 실행

개발 환경에서는 다음 명령으로 실행합니다.

```powershell
dotnet restore
dotnet run --project ImageGen.csproj
```

Visual Studio를 사용한다면 `ImageGen.sln`을 열고 `ImageGen` 프로젝트를 시작 프로젝트로 실행해도 됩니다.

### 3. 초기 설정과 첫 이미지 생성

1. 앱 상단의 **API Token** 입력란에 NovelAI 액세스 토큰을 입력합니다.
2. **Generator > Prompt > Positive**에 생성할 내용을 입력합니다.
3. 필요하면 **Parameters**에서 모델, 해상도, 스텝, 샘플러와 저장 폴더를 변경합니다.
4. 화면 아래의 **Generate Image**를 누릅니다.
5. 생성 결과는 미리보기에 표시되고 설정한 저장 폴더에 PNG 파일로 저장됩니다.

기본 저장 폴더는 실행 파일과 같은 위치의 `Output/`이며, 파일명은 `generated_yyyyMMdd_HHmmss.png` 형식입니다.

## 배포 빌드

Windows x64용 단일 실행 파일을 만들려면 저장소 루트에서 `build.bat`을 실행합니다.

```powershell
.\build.bat
```

스크립트는 필요할 경우 소스 업데이트 여부를 묻고, 복원 및 self-contained Release 게시를 수행합니다. 결과물은 `Release_Output/ImageGen.exe`에 생성됩니다. 직접 게시하려면 다음 명령을 사용할 수 있습니다.

```powershell
dotnet publish ImageGen.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o Release_Output
```

## 로컬 데이터

앱은 다음 파일과 폴더를 실행 파일 기준 경로에 생성합니다.

| 경로 | 내용 |
| --- | --- |
| `settings.json` | API 토큰, 마지막 프롬프트, 생성 파라미터, 입력 이미지 및 참조 이미지 설정 |
| `character_presets.json` | 캐릭터 프롬프트 프리셋 |
| `Graphs/` | 저장한 노드 그래프 워크플로 JSON |
| `Output/` | 기본 생성 이미지 저장 폴더 |
| `Logs/` | 날짜별 오류 및 실행 로그 |

> [!WARNING]
> 현재 API 토큰은 `settings.json`에 암호화되지 않은 문자열로 저장됩니다. 공용 PC나 동기화 폴더에서는 사용을 피하고, `settings.json`을 공유하거나 버전 관리에 포함하지 마세요. 토큰이 노출되었다면 NovelAI에서 즉시 폐기하고 새 토큰을 발급하세요.

## 프로젝트 구조

```text
ImageGen/
├── Models/          # 생성 요청, 설정, 노드 및 프리셋 데이터 모델
├── Services/        # NovelAI API, 이미지 처리, 설정, 워크플로 서비스
├── ViewModels/      # Generator, Node Graph, Director Tools 화면 로직
├── Views/           # WPF 창, 사용자 컨트롤 및 마스크 편집기
├── Helpers/         # EXIF, ZIP, JSON, 로깅 및 WPF 변환 도우미
├── App.xaml         # 애플리케이션 테마와 공통 스타일
└── ImageGen.csproj  # .NET 8 WPF 프로젝트 설정
```

앱은 MVVM 형태로 구성되어 있습니다. `MainViewModel`이 일반 이미지 생성 상태를 관리하고, `NodeGraphViewModel`과 `DirectorToolsViewModel`이 각 전용 워크플로를 담당합니다. `NovelAiApiService`가 NovelAI API 통신을, `ImageGenerationWorkflow`가 생성·미리보기·저장·Anlas 비용 계산 흐름을 담당합니다.

## 개발 및 검증

```powershell
dotnet restore
dotnet build ImageGen.sln --no-restore
```

현재 저장소에는 별도의 자동화 테스트 프로젝트가 없습니다. 변경 후에는 빌드와 함께 최소한 다음 동작을 수동으로 확인하는 것을 권장합니다.

- 설정 저장 및 앱 재시작 후 복원
- Text2Image 생성과 결과 파일 저장
- Img2Img 및 Inpaint 입력 검증
- 노드 그래프 저장, 불러오기와 체인 실행
- Director Tools 결과 저장
- EXIF 이미지 열기와 드래그 앤 드롭

## 문제 해결

- **Generate Image 버튼이 비활성화됨**: API 토큰과 Positive 프롬프트를 확인하세요. Img2Img에는 원본 이미지가, Inpaint에는 원본 이미지와 유효한 마스크가 모두 필요합니다.
- **API 오류 또는 이미지 생성 실패**: NovelAI 토큰의 유효성, Anlas 잔액과 네트워크 연결을 확인하세요.
- **오류 상세 정보 확인**: 실행 파일 옆 `Logs/log_yyyyMMdd.txt`를 확인하세요. 로그를 공유하기 전 민감한 정보가 포함되지 않았는지 검토하세요.
- **저장된 이미지가 보이지 않음**: Parameters 탭의 저장 경로를 확인하거나 앱에서 저장 폴더 열기 기능을 사용하세요.

## 라이선스

이 프로젝트는 [MIT License](LICENSE)로 배포됩니다.
