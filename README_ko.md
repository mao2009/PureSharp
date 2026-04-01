# PureSharp

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

**PureSharp**는 C#에서 "참조 투명성"과 "불변성"을 강력하게 지원하여 함수형 프로그래밍의 안전성을 C#에 도입하도록 설계된 도구 모음입니다. Roslyn 분석기를 활용하여 컴파일 수준에서 견고하고 버그에 강한 코드 작성을 강제합니다.

## 핵심 개념

1.  **순수성 강제 (Purity):** 부작용이 없는 로직을 명시적으로 선언하고 기계적으로 검증합니다.
2.  **불변성 도입 (Immutability):** 직관적인 명명 규칙을 통해 언어 사양에서 부족한 "재할당 불가능한 지역 변수" 기능을 구현합니다.
3.  **안전한 제어 흐름 (Safe Flow):** 런타임 예외와 누락을 방지하기 위해 파이프라인 스타일의 조건 분기를 제공합니다.

## 주요 기능

### 1. PureSharp.Core
기본적인 특성(Attribute)과 유틸리티를 제공합니다.

*   **`[PureMethod]` 특성**: 메서드를 "순수(참조 투명)"로 선언합니다.
*   **`Fluent.If`**: `if-else` 문을 표현식으로 작성할 수 있는 유연한 인터페이스를 제공합니다.

### 2. PureSharp.Analyzers (Roslyn 분석기)
코드 작성을 실시간으로 모니터링하고 규칙 위반을 보고합니다.

#### 참조 투명성 검증 (RTxxxx)
`[PureMethod]`가 부여된 메서드가 다음 작업을 수행할 때 "오류(Error)"가 보고됩니다.
*   정적이고 가변적인(readonly가 아닌) 필드에 접근.
*   비순수 메서드(`[PureMethod]`가 없는 메서드) 호출.
*   I/O 작업(Console, File, Network 등에 접근).

#### 지역 변수 불변성 강제 (LVPxxxx)
밑줄(`_`)로 시작하는 명명 규칙을 사용하여 불변성을 구현합니다.
*   **불변성 강제**: `_`로 시작하는 지역 변수에 대한 재할당을 금지합니다(오류).
*   **초기화 강제**: `_`로 시작하는 변수는 선언 시 초기화되어야 합니다(오류).
*   **명명 제안**: 재할당된 적이 없는 변수에 밑줄(`_`)을 추가하도록 제안합니다(경고).

#### FluentIf 종료 확인 (FIFxxxx)
*   `Fluent.If`로 시작하는 체인이 `.Else()`로 올바르게 종료되었는지 검증합니다. 종료되지 않은 경우 컴파일 오류가 발생합니다.

---

## 빠른 시작

### 1. 참조 투명 메서드 작성
```csharp
using PureSharp.Core;

public class Calculator
{
    private static int _globalCache; // 가변 정적 필드

    [PureMethod]
    public int Add(int a, int b)
    {
        // OK: 계산만 수행
        return a + b; 
        
        // NG: 정적 필드 접근 시 RT0001 오류 발생
        // _globalCache = a + b; 
        
        // NG: I/O 작업 시 RT0003 오류 발생
        // Console.WriteLine(a); 
    }
}
```

### 2. 불변 지역 변수 활용
```csharp
public void Process()
{
    int _result = Calculate(); // 불변 변수로 선언
    
    // NG: 재할당 시도 시 LVP0001 오류 발생
    // _result = 10; 
    
    int count = 0; // 일반 변수
    // 제안: 재할당되지 않는 경우 "_count"로 변경하라는 경고 발생 (LVP0003)
}
```

### 3. 안전한 조건 분기 (FluentIf)
```csharp
int status = Fluent.If(score >= 80, () => 1)
                   .ElseIf(score >= 60, () => 2)
                   .Else(0); // .Else()를 누락하면 FIF0001 오류 발생
```

---

## 프로젝트 구조

*   **PureSharp.Core**: 핵심 특성과 런타임 라이브러리를 제공합니다. (.NET 10.0 / netstandard2.0)
*   **PureSharp.Analyzers**: 주요 Roslyn 분석기입니다. (netstandard2.0)
*   **PureSharp.Analyzers.Tests**: 분석기 동작을 검증하기 위한 단위 테스트입니다. (xUnit)

---

## 개발 동기
C#은 매우 강력한 언어이지만, 대규모 개발이나 복잡한 로직에서 의도치 않은 부작용이나 변수 재사용으로 인해 디버깅이 어려워질 수 있습니다. PureSharp은 개발자에게 "제약"이라는 이름의 "자유(버그로부터의 해방)"를 제공하기 위해 탄생했습니다.

---
## 라이선스
이 프로젝트는 MIT 라이선스에 따라 배포됩니다.
