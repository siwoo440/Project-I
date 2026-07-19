프로젝트 I — 13일차 개발 일지
단계: Phase 1 — 프로토타입 (싱글 핵심 루프) 목표: 무기 4종 & 방어 (기획서 PART 4.5.2)

오늘의 목표
손에 든 무기에 따라 실제로 다르게 전투가 동작하게 한다. 검(빠름)/도끼(느림+치명타)/활(원거리+화살소모)/방패(우클릭 막기).

한 일
1. WeaponData (신규)
무기 종류(Sword/Axe/Bow/Shield) + 공격력/쿨다운/사거리/치명타확률/치명타배수/막기감소율 정의(ScriptableObject)
2. PickupItem 확장
WeaponData weaponData 필드 추가, Weapon 프로퍼티로 노출 → PlayerCombat이 손에 든 아이템의 무기 데이터를 조회
3. InventorySystem — 아이템 소모 API
ConsumeItemByName(string): 이름이 일치하는 아이템 1개를 인벤토리에서 제거·파괴 (화살 소모용)
4. PlayerCombat 재작성 — 무기별 분기
검/도끼(근접): 기존 SphereCast 판정 유지, 무기 수치(공격력·사거리·치명타) 적용
활(원거리): 우선 화살 1개 소모(ConsumeItemByName("화살"), 없으면 발사 취소) → Raycast로 원거리 명중 판정
방패: 좌클릭 공격 불가, 우클릭 누르는 동안 IsBlocking/CurrentBlockReduction 활성화
무기 없음(맨손) 시 기본 약한 공격치 사용
5. PlayerController 연동
TakeDamage()에서 PlayerCombat.CurrentBlockReduction을 조회해 방패 막기 중이면 받는 피해를 비율만큼 감소
결과
무기별 공격 속도·데미지·치명타 차이 확인(도끼 치명타 시 큰 피해)
활로 원거리 몬스터 명중, 화살 소모 및 소진 시 발사 불가 확인
방패 우클릭 시 몬스터 공격 피해 감소(막기) 확인
다음 (14일차 예정)
플레이어 피격·사망·부활석 — 사망 처리(조작 불가), 부활의 돌 1회 자동 소생
참고 / TODO
활은 즉시 명중(레이캐스트) 방식 — 날아가는 화살 이펙트는 아트 단계에서
방패 막기는 방향 판정 없이 "누르는 동안 전방위 감소" — 이후 정교화 가능