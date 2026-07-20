\# 38일차 개발 일지



\## 작업 주제



Scene과 플레이어 및 전투 오디오 피드백 연결



\## 작업 목표



\- Scene별 BGM 재생

\- Scene별 환경음 재생

\- 플레이어 발소리 구현

\- 플레이어 피격과 사망음 연결

\- 맨손과 무기별 공격음 연결

\- 공격 명중 효과음 연결

\- 공통 상호작용 효과음 연결

\- 몬스터 3D 효과음을 AudioMixer SFX 그룹에 연결



\## 구현 내용



\### AudioManager SFX 그룹 연결



기존 AudioManager가 생성하는 풀링 AudioSource를 ProjectI\_AudioMixer의 SFX 그룹으로 출력하도록 수정했다.



GameAudioManager는 BGM과 Ambient 및 플레이어 중심의 2D SFX를 담당하고, 기존 AudioManager는 몬스터와 월드 오브젝트의 3D 위치 효과음을 담당한다.



\### Scene 오디오



MainMenu, Village, Dungeon의 SceneAudioController에 Scene별 BGM과 환경음을 연결할 수 있도록 구성했다.



Scene이 시작되면 해당 BGM과 환경음을 반복 재생하고 Scene 종료 시 기존 소리를 정지한다.



\### PlayerAudioFeedback



Player 루트에 PlayerAudioFeedback을 추가했다.



다음 플레이어 이벤트에 효과음을 연결했다.



\- 걷기 발소리

\- 달리기 발소리

\- 일반 피해

\- 최종 사망

\- 맨손 공격

\- 검 공격

\- 도끼 공격

\- 활 발사

\- 몬스터 명중

\- E 상호작용



발소리는 CharacterController의 수평 이동속도와 접지 상태를 사용한다.



걷기와 달리기에 서로 다른 발소리 간격을 적용하고 정지, 점프, 일시정지 중에는 발소리를 재생하지 않는다.



\### PlayerCombat 이벤트



PlayerCombat에 AttackPerformed와 AttackHit 이벤트를 추가했다.



공격 판정과 피해 계산은 변경하지 않고 공격 실행과 명중 사실만 PlayerAudioFeedback에 전달한다.



활은 실제 화살 소모에 성공했을 때만 발사 이벤트를 전달한다.



방패는 공격할 수 없으므로 공격 이벤트를 전달하지 않는다.



\### PlayerInteractor 이벤트



PlayerInteractor에 Interacted 이벤트를 추가했다.



현재 상호작용 대상이 존재하는 상태에서 E 입력이 실행됐을 때 공통 상호작용 효과음을 재생한다.



\### 몬스터 오디오



기존 ThreatFeedback의 Warning, Appear, Attack, Hit AudioClip을 사용했다.



몬스터 효과음은 AudioManager의 풀링 AudioSource를 통해 3D 위치에서 재생된다.



SFX 음량 설정이 몬스터 효과음에도 적용되도록 AudioMixerGroup을 연결했다.



\## 테스트 결과



\- MainMenu BGM 재생 정상

\- Village BGM과 환경음 재생 정상

\- Dungeon BGM과 환경음 재생 정상

\- Scene 이동 후 이전 오디오 정지 정상

\- 걷기 발소리 정상

\- 달리기 발소리 간격 변경 정상

\- 점프 중 발소리 차단 정상

\- 일시정지 중 발소리 차단 정상

\- 맨손 공격음 정상

\- 검과 도끼 공격음 정상

\- 활 발사음 정상

\- 화살 부족 시 발사음 차단 정상

\- 몬스터 명중음 정상

\- 플레이어 피격과 사망음 정상

\- E 상호작용 공통 효과음 정상

\- 몬스터 공격과 피격 3D 효과음 정상

\- Master, BGM, SFX, Ambient 음량 분리 정상



\## 다음 작업



39일차에는 기존 OnGUI 플레이어 HUD를 Canvas 기반 정식 HUD로 교체한다.



구현 대상은 다음과 같다.



\- 체력

\- 스태미너

\- 밝기

\- 제한시간

\- 조준점

\- 기본 조작 안내

