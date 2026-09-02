using System; // 예외와 정렬 기능 사용
using System.IO; // 저장 파일 읽기·쓰기 기능 사용
using System.Linq; // 스냅샷 파일 정렬 기능 사용
using System.Security.Cryptography; // SHA-256 무결성 검사 기능 사용
using System.Text; // UTF-8 바이트 변환 기능 사용
using UnityEngine; // JsonUtility와 persistentDataPath 사용

namespace ProjectI.Persistence // 일차 저장·복구 네임스페이스
{
    public sealed class DailySnapshotStore // 로컬 Current와 불변 일차 스냅샷 파일 저장소
    {
        private const int EnvelopeSchemaVersion = 1; // 현재 외곽 저장 형식 버전
        private readonly string rootPath; // Project I 저장 루트 경로
        private readonly string currentPath; // 현재 일차 시작 데이터 파일 경로
        private readonly string dailyFolder; // 불변 일차 스냅샷 폴더

        public string CurrentPath => currentPath; // 진단용 Current 파일 경로 공개
        public string DailyFolder => dailyFolder; // 진단용 Daily 폴더 경로 공개

        public DailySnapshotStore() // 기본 persistentDataPath 저장소 생성
        {
            rootPath = Path.Combine(Application.persistentDataPath, "ProjectI", "Saves"); // 게임별 저장 루트 설정
            currentPath = Path.Combine(rootPath, "Current", "current.json"); // 현재 일차 시작 데이터 경로 설정
            dailyFolder = Path.Combine(rootPath, "DailySnapshots"); // 완료 일차 백업 폴더 설정
        }

        public bool CurrentFileExists() // Current 파일 존재 여부 반환
        {
            return File.Exists(currentPath); // 실제 파일 시스템 확인
        }

        public bool WriteCurrent(DailySnapshotData data) // 현재 일차 시작 데이터를 교체 저장
        {
            if (data == null) // 저장 대상 유효성 확인
            {
                return false; // null 데이터 저장 차단
            }

            string envelopeJson = BuildEnvelopeJson(data); // SHA-256 포함 JSON 생성
            return WriteAtomic(currentPath, envelopeJson, true); // Current는 안전한 교체 저장 허용
        }

        public bool WriteImmutableDailySnapshot(DailySnapshotData data) // 완료 일차 스냅샷을 한 번만 저장
        {
            if (data == null || data.completedDay <= 0) // 완료 일차 데이터인지 확인
            {
                return false; // 잘못된 일차 저장 차단
            }

            Directory.CreateDirectory(dailyFolder); // 일차 백업 폴더 보장
            string path = GetDailyPath(data.completedDay); // 해당 완료 일차 파일 경로 계산

            if (File.Exists(path)) // 이미 같은 일차 백업이 존재하는지 확인
            {
                return false; // 기존 정상·손상 여부와 관계없이 같은 일차는 절대 덮어쓰거나 재완료하지 않음
            }

            string envelopeJson = BuildEnvelopeJson(data); // 새 완료 일차 SHA-256 JSON 생성
            return WriteAtomic(path, envelopeJson, false); // 기존 파일을 덮지 않고 최초 1회만 기록
        }

        public bool TryReadCurrent(out DailySnapshotData data, out string reason) // Current 파일 무결성 검사 후 읽기
        {
            return TryReadPath(currentPath, out data, out reason); // 공통 검증 로더 사용
        }

        public bool TryReadLatestValidDailySnapshot(out DailySnapshotData data, out string path) // 가장 최근 정상 일차 스냅샷 검색
        {
            data = null; // 기본 결과 초기화
            path = string.Empty; // 기본 경로 초기화

            if (!Directory.Exists(dailyFolder)) // 백업 폴더 존재 여부 확인
            {
                return false; // 저장된 일차 없음 반환
            }

            string[] files = Directory.GetFiles(dailyFolder, "Day_*.json", SearchOption.TopDirectoryOnly) // 일차 스냅샷 파일 조회
                .OrderByDescending(ParseDayFromPath) // 일차 번호가 큰 파일부터 검사
                .ToArray(); // 안정된 순회 배열 생성

            foreach (string candidate in files) // 최신 파일부터 순회
            {
                if (!TryReadPath(candidate, out DailySnapshotData candidateData, out _)) // SHA-256 또는 JSON 검증 실패 여부 확인
                {
                    continue; // 손상 스냅샷은 건너뜀
                }

                data = candidateData; // 가장 최근 정상 데이터 반환
                path = candidate; // 복구에 사용한 파일 경로 반환
                return true; // 정상 스냅샷 검색 성공
            }

            return false; // 사용할 수 있는 정상 백업 없음
        }

        public string ReadEnvelopeTextForRemote(int completedDay) // 향후 서버 업로드에 사용할 검증 포함 원문 조회
        {
            string path = GetDailyPath(completedDay); // 완료 일차 파일 경로 계산
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty; // 존재할 때만 원문 반환
        }

        private string BuildEnvelopeJson(DailySnapshotData data) // 데이터와 SHA-256을 하나의 JSON으로 구성
        {
            string payloadJson = JsonUtility.ToJson(data, false); // 실제 게임 상태 JSON 생성
            DailySnapshotEnvelope envelope = new DailySnapshotEnvelope // 검증 외곽 데이터 생성
            {
                schemaVersion = EnvelopeSchemaVersion, // 외곽 형식 버전 기록
                checksum = ComputeHash(payloadJson), // payload SHA-256 계산
                payloadJson = payloadJson // 검증 대상 원문 저장
            };
            return JsonUtility.ToJson(envelope, false); // 파일에 기록할 최종 JSON 반환
        }

        private bool TryReadPath(string path, out DailySnapshotData data, out string reason) // 파일 단위 검증·역직렬화 공통 처리
        {
            data = null; // 실패 기본값 설정
            reason = string.Empty; // 실패 이유 초기화

            if (!File.Exists(path)) // 파일 존재 여부 확인
            {
                reason = "파일 없음"; // 누락 이유 기록
                return false; // 읽기 실패 반환
            }

            try // 손상 파일 예외를 복구 흐름으로 전환
            {
                string envelopeJson = File.ReadAllText(path, Encoding.UTF8); // UTF-8 원문 읽기
                DailySnapshotEnvelope envelope = JsonUtility.FromJson<DailySnapshotEnvelope>(envelopeJson); // 외곽 JSON 해석

                if (envelope == null || string.IsNullOrWhiteSpace(envelope.payloadJson) || string.IsNullOrWhiteSpace(envelope.checksum)) // 필수 필드 확인
                {
                    reason = "Envelope 필드 손상"; // 구조 손상 이유 기록
                    return false; // 검증 실패 반환
                }

                string actualChecksum = ComputeHash(envelope.payloadJson); // 실제 payload SHA-256 다시 계산

                if (!string.Equals(actualChecksum, envelope.checksum, StringComparison.OrdinalIgnoreCase)) // 저장된 해시와 비교
                {
                    reason = "SHA-256 불일치"; // 데이터 변조·손상 이유 기록
                    return false; // 손상 파일 사용 금지
                }

                DailySnapshotData parsed = JsonUtility.FromJson<DailySnapshotData>(envelope.payloadJson); // 검증 통과 payload 역직렬화

                if (parsed == null || parsed.schemaVersion <= 0) // 실제 게임 데이터 구조 유효성 확인
                {
                    reason = "Snapshot payload 손상"; // 파싱 실패 이유 기록
                    return false; // 사용할 수 없는 데이터 반환 금지
                }

                parsed.items ??= new System.Collections.Generic.List<ProjectI.Items.ItemInstanceData>(); // 이전 빈 목록 안전 보정
                parsed.economy ??= new EconomySnapshotData(); // 이전 경제 데이터 누락 안전 보정
                data = parsed; // 정상 데이터 반환
                return true; // 검증 성공 반환
            }
            catch (Exception exception) // 파일 I/O 또는 JSON 예외 처리
            {
                reason = exception.Message; // 진단용 예외 메시지 저장
                return false; // 손상 파일로 판단
            }
        }

        private bool WriteAtomic(string path, string text, bool replaceExisting) // 임시 파일 후 교체하는 안전 저장
        {
            try // I/O 실패를 호출자 성공 여부로 반환
            {
                string directory = Path.GetDirectoryName(path); // 대상 상위 폴더 계산
                Directory.CreateDirectory(directory); // 저장 폴더 생성 보장
                string tempPath = path + ".tmp"; // 동일 폴더 임시 파일 경로 생성
                File.WriteAllText(tempPath, text, new UTF8Encoding(false)); // 먼저 완전한 임시 파일 기록

                if (!File.Exists(path)) // 최초 생성인지 확인
                {
                    File.Move(tempPath, path); // 완성된 임시 파일을 최종 이름으로 이동
                    return true; // 최초 저장 성공
                }

                if (!replaceExisting) // 불변 파일 덮어쓰기 금지 여부 확인
                {
                    File.Delete(tempPath); // 사용하지 않을 임시 파일 제거
                    return false; // 기존 일차 파일 유지
                }

                string backupPath = path + ".replace.bak"; // File.Replace 임시 백업 경로

                if (File.Exists(backupPath)) // 이전 교체 백업 잔여 확인
                {
                    File.Delete(backupPath); // 오래된 교체 백업 제거
                }

                try // 지원 플랫폼에서는 원자적 File.Replace 우선 사용
                {
                    File.Replace(tempPath, path, backupPath, true); // 임시 파일을 현재 파일과 원자적으로 교체

                    if (File.Exists(backupPath)) // 교체 성공 후 임시 백업 확인
                    {
                        File.Delete(backupPath); // 교체용 백업 정리
                    }
                }
                catch (PlatformNotSupportedException) // File.Replace 미지원 플랫폼 대응
                {
                    File.Delete(path); // 기존 Current 제거
                    File.Move(tempPath, path); // 완성된 임시 파일을 최종 경로로 이동
                }
                catch (IOException) // 일부 파일 시스템 File.Replace 실패 대응
                {
                    File.Delete(path); // 기존 Current 제거
                    File.Move(tempPath, path); // 완성된 임시 파일을 최종 경로로 이동
                }

                return true; // 교체 저장 성공 반환
            }
            catch (Exception exception) // 저장 실패 예외 처리
            {
                Debug.LogError($"[Project I] 일차 스냅샷 파일 저장 실패 / {path} / {exception.Message}"); // 저장 실패 로그 출력
                return false; // 호출자에게 실패 반환
            }
        }

        private string GetDailyPath(int day) // 완료 일차 번호에서 고정 파일 경로 생성
        {
            return Path.Combine(dailyFolder, $"Day_{Math.Max(1, day):000}.json"); // 3자리 일차 파일명 반환
        }

        private static int ParseDayFromPath(string path) // 파일명에서 일차 번호 추출
        {
            string name = Path.GetFileNameWithoutExtension(path); // 확장자를 제외한 파일명 조회
            string numeric = name.StartsWith("Day_", StringComparison.OrdinalIgnoreCase) ? name.Substring(4) : "0"; // Day_ 접두어 제거
            return int.TryParse(numeric, out int day) ? day : 0; // 숫자 파싱 실패 시 가장 오래된 0으로 처리
        }

        private static string ComputeHash(string text) // payload SHA-256 16진수 계산
        {
            using (SHA256 sha = SHA256.Create()) // SHA-256 해시 객체 생성
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty); // UTF-8 바이트 배열 생성
                byte[] hash = sha.ComputeHash(bytes); // 실제 SHA-256 계산
                StringBuilder builder = new StringBuilder(hash.Length * 2); // 16진수 문자열 버퍼 생성

                foreach (byte value in hash) // 해시 바이트 순회
                {
                    builder.Append(value.ToString("x2")); // 소문자 2자리 16진수 추가
                }

                return builder.ToString(); // 최종 SHA-256 문자열 반환
            }
        }
    }
}
