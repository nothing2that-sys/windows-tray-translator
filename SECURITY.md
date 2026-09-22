# Security Policy

## Supported versions

보안 수정은 최신 공개 minor release에 우선 적용합니다. Public Release 전에는 `main`의 최신 코드만 검토 대상입니다.

## Reporting a vulnerability

API Key 노출, 개인정보 노출, 임의 코드 실행 또는 기타 보안 문제는 공개 Issue에 상세 내용을 게시하지 말고 GitHub 저장소의 **Security > Advisories > New draft security advisory**를 통해 비공개로 신고해 주세요.

신고에는 가능한 범위에서 영향받는 버전, 재현 절차, 예상 영향과 완화 방법을 포함해 주세요. 실제 API Key, 개인 데이터 또는 제3자의 민감 정보는 첨부하지 마십시오. API Key가 노출되었다면 저장소 수정과 별개로 즉시 해당 키를 폐기하고 새 키를 발급해야 합니다.

접수된 취약점은 영향과 재현 가능성을 확인한 뒤 수정 및 공개 일정을 신고자와 조율합니다. 수정본이 준비되기 전에는 공개를 유예해 주세요.

## Dependency security

NuGet 및 GitHub Actions 의존성은 Dependabot으로 정기 확인합니다. 심각도가 높은 유효 취약점은 우선적으로 최신 지원 버전에 반영합니다.
