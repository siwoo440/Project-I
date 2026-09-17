# 40일차 — Steam 로비·친구 초대·재접속

## 이번 일차 목표

39일차까지는 **주소(IP)를 알아야** 함께할 수 있었다. 40일차에는 Steam을 통해 방을 찾고, 친구를 초대하고, 끊기면 다시 붙게 만든다.

```text
방장: 방 만들기 → Steam 로비 생성(게임·버전·방 이름·방장 ID) → Steam 중계망으로 연결 대기
참가자: 서버 창에 Steam 방 목록 → 참가 → 로비 입장 → 방장 Steam ID 로 P2P 연결
친구: Steam 친구 목록 "게임 참가"·초대 수락 → (게임 중이면 저장 후 메뉴) → 같은 방 참가
```

## 1. Steamworks.NET

- `Packages/manifest.json`에 `com.rlabrecque.steamworks.net` 2025.164.1 (git)을 추가했다. 패키지가 `STEAMWORKS_NET` 정의를 넣는다.
- 프로젝트 루트의 `steam_appid.txt`에 Steam 공용 시험 앱 **480**(Spacewar)을 적었다. 실제 앱 ID를 받으면 이 값과 `SteamService.TestAppId`를 교체한다.

## 2. `SteamService` — 초기화

- 게임 시작 시(`AfterSceneLoad`) 순서대로 진행한다.
  1. `Packsize`·`DllCheck`로 환경을 확인한다.
  2. `SteamAPI.Init`로 Steam에 연결한다.
  3. `InitRelayNetworkAccess`로 중계망을 준비한다.
- 연결되면 유지 오브젝트 `===ProjectI Steam===`가 매 프레임 `RunCallbacks`를 호출하고, 종료할 때 로비를 나가며 `Shutdown`한다.
- 실패하면 이유(`FailureReason`)를 남기고 **직접 IP 연결만** 사용한다.
- 에디터에서 플레이를 반복해도 정적 상태를 초기화한다(`SubsystemRegistration`).

## 3. `SteamNetworkTransport` — 넷코드 연결 모듈

`NetworkTransport`를 상속해 직접 만든 Steam 네트워킹 소켓 P2P 연결 모듈이다. **포트 개방 없이** Steam 중계망으로 연결한다.

| 항목 | 내용 |
| --- | --- |
| 방장 | `CreateListenSocketP2P` → 들어오는 연결 `AcceptConnection` |
| 참가자 | `ConnectP2P(HostId)` (방장 서버 ID = 0) |
| 상태 콜백 | 연결됨 → Connect 이벤트 · 상대가 끊음/문제 발생 → Disconnect 이벤트 후 닫기 |
| 전송 | 넷코드 신뢰/비신뢰 → Steam Reliable/Unreliable + NoNagle |
| 수신 | 이벤트 대기열 먼저, 그다음 연결별 `ReceiveMessagesOnConnection` |
| 지연 | `GetConnectionRealTimeStatus` 핑 |

- 네트워크 프리팹은 그대로 두고, 실행 중에 `NetworkSession`이 이 컴포넌트를 붙인다.

## 4. `SteamLobbyService` — 로비

- **만들기**: 로비 데이터에 게임·버전(`ProjectI-0.40`)·방 이름(`○○의 원정대`)·방장 ID·위치(지연 추정)를 적는다.
- **갱신**: 방장은 5초마다 인원·일차·도전 정보를 갱신한다. 가득 차면 참가할 수 없게 한다.
- **목록**: 같은 게임·버전만 전 세계에서 50개까지 찾는다. 서버 창 형식(`ServerListing`, 지역 `STEAM`, 추정 핑)으로 바꾼다.
- **들어가기**: 버전이 다르면 거절하고, 방장 ID(로비 데이터 → 없으면 소유자)를 넘긴다.
- **초대**: `OpenInviteOverlay`로 Steam 초대 창을 연다. `GameLobbyJoinRequested_t`로 친구 참가 요청을 받고, `+connect_lobby` 실행 인자도 처리한다.

## 5. 서버 창·메인 메뉴

- `SteamServerListProvider`: Steam이 연결되어 있으면 예시 목록 대신 **실제 Steam 방 목록**을 쓴다. `참가`를 누르면 `NetworkSession.BeginSteamJoin`을 호출한다.
- `[ 방 만들기 ]`는 Steam이 있으면 Steam 방, 없으면 포트 방을 연다. 안내 문구에 방식이 표시된다.
- 메뉴 아래에 `Steam 연결됨 · 이름` 또는 `Steam 미연결 — 주소로만 참가 (이유)`를 표시한다.
- 게임 중 받은 참가 요청이나 초대로 게임을 켠 경우, 메뉴에 들어오면 서버 창을 열고 바로 참가한다.

## 6. `NetworkSession` — 방식 선택·재접속

- `SessionTransport { Direct, Steam }`: 방 열기·참가할 때 넷코드의 연결 모듈을 바꿔 끼운다.
- `QueueSteamJoin`: 메뉴에서는 바로 참가한다. 게임 중에는 사무실이면 저장한 뒤 메뉴로 돌아가 참가한다.
- **재접속**: 참가자가 게임 중에 끊기면(직접 IP이거나 아직 Steam 로비 안) **최대 3번** 다시 시도한다.
  - 시도마다 연결을 종료하고 정리를 기다린 뒤(최대 4초), 2초 후 다시 연결한다.
  - 다시 연결되면 월드를 새로 불러오지 않고 `다시 연결됨`을 표시한다. 38·39일차의 전체 상태 요청이 차이를 맞춘다.
  - 모두 실패하면 로비를 나가고 메인 메뉴에서 이유를 표시한다.
- 방장 알림: `원정대원 참가 (n/4)` / `원정대원 한 명이 나갔습니다`

## 7. 이름·초대 버튼

- `NetPlayerAvatar`: 소유자가 `NetworkVariable<FixedString64Bytes>`에 Steam 이름을 적는다. 긴 이름은 글자 단위로 자른다. 이름표는 Steam 이름을 우선 쓰고, 없으면 `원정대원 N`으로 표시한다.
- `PauseMenu`: Esc 창에 **친구 초대 (Steam)** 버튼을 추가했다. Steam 방에 있을 때만 보인다.

## 8. 빌드·버전

- 접속 확인 `ProjectI-0.40`, 빌드 `0.40.0-alpha`
- Windows 알파 빌드가 끝나면 실행 파일 옆에 `steam_appid.txt`를 복사한다. Steam 스토어로 배포할 때는 빼야 한다.

## 검증

```text
컴파일        Unity 밖 dotnet (Steamworks.NET + Netcode DLL) — 오류 0
              Unity 에디터 패키지 설치·컴파일 — 새 오류 없음
빌드          Windows 알파 0.40.0-alpha Succeeded · 127.9MB · 오류 0
              steam_appid.txt · Plugins/x86_64/steam_api64.dll 포함 확인
Steam 실제    미확인 — 서로 다른 Steam 계정 2개·PC 2대 필요
2인 확인      37~40일차 함께 확인 필요
```

## 남은 것

- 서로 다른 계정 2개로 Steam 방 목록·참가·초대·재접속 실제 확인
- 시험 앱 480은 다른 개발자 로비도 섞인다 (게임·버전 키로 거름). 실제 앱 ID가 생기면 교체한다.
- Steam 로비 안에서 연결이 끊긴 뒤 방장이 로비를 떠난 경우의 처리 다듬기
- 41일차: 방 코드로 입장
