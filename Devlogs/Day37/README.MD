\# 37일차 개발 일지



\## 작업 주제



AudioMixer와 전역 오디오 관리 및 Scene AudioClip 재생 기반 구현



\## 작업 목표



\- 프로젝트 공통 AudioMixer 생성

\- Master, BGM, SFX, Ambient 그룹 분리

\- 그룹별 음량 조절 기능 구현

\- 전역 GameAudioManager 구현

\- 오디오 설정값 저장

\- GameSettingsUI 음량 설정 확장

\- Scene별 AudioClip 재생 구조 구현

\- 보유한 AudioClip의 초기 재생 테스트



\## 구현 내용



\### ProjectI\_AudioMixer



프로젝트 전체 오디오를 관리하는 AudioMixer를 생성했다.



AudioMixer는 다음 그룹으로 구성했다.



\- Master

\- BGM

\- SFX

\- Ambient



BGM, SFX, Ambient 그룹은 Master의 자식으로 배치했다.



Master 음량을 줄이면 모든 오디오가 함께 줄어들고, 하위 그룹은 각 종류의 오디오에만 영향을 주도록 구성했다.



\### Exposed Parameters



스크립트에서 AudioMixer 음량을 변경할 수 있도록 다음 Volume Parameter를 노출했다.



\- MasterVolume

\- BGMVolume

\- SFXVolume

\- AmbientVolume



사용자가 설정 화면에서 선택한 0에서 1 사이의 음량을 데시벨 값으로 변환하여 AudioMixer에 적용한다.



음량 변환 기준은 다음과 같다.



\- 0%: -80dB

\- 100%: 0dB



\### GameAudioManager



프로젝트 전체 오디오를 관리하는 GameAudioManager를 구현했다.



GameAudioManager는 싱글톤과 DontDestroyOnLoad를 사용하여 MainMenu, Village, Dungeon 사이에서 유지된다.



GameAudioManager 아래에 다음 AudioSource를 구성했다.



\- BGMSource

\- AmbientSource

\- SFXSource



각 AudioSource는 다음 AudioMixerGroup에 연결했다.



\- BGMSource → BGM

\- AmbientSource → Ambient

\- SFXSource → SFX



GameAudioManager에 다음 오디오 재생 API를 추가했다.



\- PlayBgm

\- StopBgm

\- PlayAmbient

\- StopAmbient

\- PlaySfx

\- StopAllAudio

\- ApplyVolumes



PlayBgm과 PlayAmbient는 반복 재생을 지원한다.



PlaySfx는 PlayOneShot을 사용하여 기존 효과음을 중단하지 않고 새로운 효과음을 재생한다.



\### GameSettingsManager 확장



기존 전체 음량 설정을 다음 네 종류로 확장했다.



\- 전체 음량

\- 배경음 음량

\- 효과음 음량

\- 환경음 음량



각 음량은 PlayerPrefs에 개별적으로 저장한다.



게임을 종료하거나 새 게임을 시작해도 사용자 오디오 설정이 유지된다.



AudioMixer와 AudioListener.volume이 동시에 적용되어 음량이 이중으로 감소하지 않도록 AudioListener.volume은 1로 고정했다.



\### GameSettingsUI 확장



기존 설정 화면에 다음 음량 슬라이더를 추가했다.



\- 전체 음량

\- 배경음

\- 효과음

\- 환경음



메인 메뉴와 일시정지 메뉴에서 동일한 GameSettingsUI를 사용한다.



사용자가 적용 및 저장을 누르면 네 종류의 음량을 저장하고 AudioMixer에 즉시 적용한다.



\### SceneAudioController



Scene별 배경음과 환경음을 재생하기 위한 SceneAudioController를 구현했다.



SceneAudioController는 다음 AudioClip을 연결할 수 있다.



\- Bgm Clip

\- Ambient Clip

\- Startup Sfx Clip



Bgm Clip은 Scene 시작 시 BGM 그룹으로 반복 재생한다.



Ambient Clip은 Scene 시작 시 Ambient 그룹으로 반복 재생한다.



Startup Sfx Clip은 Scene 시작 시 SFX 그룹으로 한 번 재생한다.



Scene이 종료될 때 설정에 따라 현재 배경음과 환경음을 정지할 수 있도록 구성했다.



\### 초기 AudioClip 적용



보유한 AudioClip을 종류에 따라 다음 폴더에서 관리하도록 구성했다.



\- Assets/Audio/BGM

\- Assets/Audio/Ambient

\- Assets/Audio/SFX



긴 BGM과 환경음에는 Streaming 방식을 사용하고, 짧은 효과음에는 Decompress On Load 방식을 사용하도록 설정했다.



SceneAudioController의 해당 필드에 AudioClip을 연결하여 초기 재생을 확인했다.



BGM을 적용한 경우 다음 흐름으로 재생된다.



SceneAudioController

→ GameAudioManager.PlayBgm

→ BGMSource

→ ProjectI\_AudioMixer의 BGM 그룹

→ Master 그룹

→ AudioListener



\### Scene 구성



MainMenu, Village, Dungeon에 PF\_GameAudioManager를 배치했다.



정상적인 게임 실행에서는 MainMenu의 GameAudioManager가 유지되고, 이후 Scene의 중복 관리자는 자동으로 제거된다.



각 Scene을 직접 실행하는 개발 테스트에서도 오디오 관리자가 존재하도록 구성했다.



SceneAudioController는 실제 AudioClip을 재생할 Scene에만 배치한다.



\## Hierarchy



\### GameAudioManager



GameAudioManager

├─ BGMSource

├─ AmbientSource

└─ SFXSource



\### MainMenu 예시



MainMenu

├─ Main Camera

└─ MainMenuSystems

&#x20;  ├─ CampaignSaveManager

&#x20;  ├─ GameSettingsManager

&#x20;  ├─ GameAudioManager

&#x20;  │  ├─ BGMSource

&#x20;  │  ├─ AmbientSource

&#x20;  │  └─ SFXSource

&#x20;  ├─ SceneAudioController

&#x20;  ├─ MainMenuUI

&#x20;  └─ GameSettingsUI



\## AudioSource 설정



\### BGMSource



\- Output: ProjectI\_AudioMixer / BGM

\- Play On Awake: 비활성화

\- Loop: 활성화

\- Volume: 1

\- Pitch: 1

\- Spatial Blend: 0



\### AmbientSource



\- Output: ProjectI\_AudioMixer / Ambient

\- Play On Awake: 비활성화

\- Loop: 활성화

\- Volume: 1

\- Pitch: 1

\- Spatial Blend: 0



\### SFXSource



\- Output: ProjectI\_AudioMixer / SFX

\- Play On Awake: 비활성화

\- Loop: 비활성화

\- Volume: 1

\- Pitch: 1

\- Spatial Blend: 0



\## 테스트 결과



\- AudioMixer 생성 정상

\- Master 그룹 생성 정상

\- BGM 그룹 생성 정상

\- SFX 그룹 생성 정상

\- Ambient 그룹 생성 정상

\- Exposed Parameter 연결 정상

\- GameAudioManager 싱글톤 정상

\- Scene 전환 후 오디오 관리자 유지 정상

\- 중복 GameAudioManager 제거 정상

\- SceneAudioController의 BGM 재생 정상

\- BGM 반복 재생 정상

\- Scene 종료 시 BGM 정지 정상

\- Master 음량 조절 정상

\- BGM 개별 음량 조절 정상

\- SFX 개별 음량 조절 정상

\- Ambient 개별 음량 조절 정상

\- PlayerPrefs 음량 저장 정상

\- 게임 재실행 후 음량 복원 정상

\- 메인 메뉴 설정 화면 정상

\- 일시정지 설정 화면 정상

\- AudioListener.volume 중복 적용 방지 정상



\## 주의사항



AudioMixer의 Exposed Parameter 이름은 다음과 정확하게 일치해야 한다.



\- MasterVolume

\- BGMVolume

\- SFXVolume

\- AmbientVolume



한 Scene에는 활성화된 Audio Listener가 하나만 존재해야 한다.



GameAudioManager의 BGMSource에 AudioClip을 직접 고정하지 않고 SceneAudioController가 실행 시 연결하도록 구성했다.



SceneAudioController의 Startup Sfx Clip은 현재 재생 테스트 용도이며 실제 효과음 이벤트 연결은 다음 작업에서 진행한다.



\## 다음 작업



38일차에는 실제 게임 이벤트에 오디오를 연결한다.



주요 작업 범위는 다음과 같다.



\- MainMenu 배경음

\- Village 배경음과 환경음

\- Dungeon 배경음과 환경음

\- 플레이어 발소리

\- 무기 공격 효과음

\- 플레이어 피격 효과음

\- 아이템 상호작용 효과음

\- 문과 레버 효과음

\- 몬스터 공격과 피격 및 사망 효과음

