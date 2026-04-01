# PureSharp

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

**PureSharp** estas ilaro desegnita por forte subteni "referencan travideblecon" kaj "neŝanĝeblecon" en C#, alportante la sekurecon de funkcia programado al C#. Ĝi uzas Roslyn-analizilojn por devigi fortikan, sen-cimaran kodskribadon ĉe la kompila nivelo.

## Kernaj Konceptoj

1.  **Devigo de Pureco:** Eksplicite deklaras logikon sen flankaj efikoj kaj verifikas ĝin meĥanike.
2.  **Enkonduko de Neŝanĝebleco:** Atingas "ne-reasmigeblajn lokajn variablojn", trajto mankanta en la lingva specifo, per intuiciaj nomumaj konvencioj.
3.  **Sekura Kontrola Fluo:** Provizas duktostilan kondiĉan branĉiĝon por malhelpi rultempajn esceptojn kaj malatentojn.

## Ĉefaj Trajtoj

### 1. PureSharp.Core
Provizas fundamentajn atributojn kaj ilojn.

*   **`[PureMethod]` atributo**: Deklaras metodon kiel "pura" (reference travidebla).
*   **`Fluent.If`**: Fluida interfaco kiu permesas skribi `if-else` instrukciojn kiel esprimojn.

### 2. PureSharp.Analyzers (Roslyn-analiziloj)
Monitoras kodskribadon en reala tempo kaj raportas regulmalobeojn.

#### Verifiko de Referenca Travidebleco (RTxxxx)
"Eraro" (Error) estas raportita kiam metodo markita per `[PureMethod]` plenumas la sekvajn operaciojn:
*   Aliro al senmovaj (static), ŝanĝeblaj (ne-readonly) kampoj.
*   Voko de ne-puraj metodoj (metodoj sen `[PureMethod]`).
*   I/O operacioj (aliro al Console, File, Network, ktp.).

#### Devigo de Loka Variabla Neŝanĝebleco (LVPxxxx)
Atingas neŝanĝeblecon uzante nomuman konvencion kiu komenciĝas per substreko (`_`).
*   **Deviga Neŝanĝebleco**: Malpermesas reasignon al lokaj variabloj komenciĝantaj per `_` (Eraro).
*   **Deviga Inicialigo**: Variabloj komenciĝantaj per `_` devas esti inicialigitaj dum deklaro (Eraro).
*   **Nomuma Sugesto**: Sugestas aldoni substrekon (`_`) al variabloj kiuj neniam estis reasignitaj (Averto).

#### Kontrolo de FluentIf-Terminiĝo (FIFxxxx)
*   Verifikas ke ĉenoj komenciĝantaj per `Fluent.If` estas ĝuste terminitaj per `.Else()`. Malsukceso termini rezultigos kompileraran eraron.

---

## Rapida Komenco

### 1. Skribado de Reference Travideblaj Metodoj
```csharp
using PureSharp.Core;

public class Calculator
{
    private static int _globalCache; // Ŝanĝebla senmova kampo

    [PureMethod]
    public int Add(int a, int b)
    {
        // Bone: Nur kalkulo
        return a + b; 
        
        // Malbone: Aliro al senmova kampo kaŭzas RT0001 eraron
        // _globalCache = a + b; 
        
        // Malbone: I/O operacioj kaŭzas RT0003 eraron
        // Console.WriteLine(a); 
    }
}
```

### 2. Uzado de Neŝanĝeblaj Lokaj Variabloj
```csharp
public void Process()
{
    int _result = Calculate(); // Deklarita kiel neŝanĝebla variablo
    
    // Malbone: Provo reasigno kaŭzas LVP0001 eraron
    // _result = 10; 
    
    int count = 0; // Regula variablo
    // Sugesto: Se ne reasignita, averto por ŝanĝi al "_count" (LVP0003)
}
```

### 3. Sekura Kondiĉa Branĉiĝo (FluentIf)
```csharp
int status = Fluent.If(score >= 80, () => 1)
                   .ElseIf(score >= 60, () => 2)
                   .Else(0); // Forgesi .Else() kaŭzas FIF0001 eraron
```

---

## Projekta Strukturo

*   **PureSharp.Core**: Provizas kernajn atributojn kaj rultempajn bibliotekojn (.NET 10.0 / netstandard2.0).
*   **PureSharp.Analyzers**: La ĉefa Roslyn-analizilo (netstandard2.0).
*   **PureSharp.Analyzers.Tests**: Unuon-testoj por verifiki analizilan konduton (xUnit).

---

## Motivo por Evoluigo
C# estas tre potenca lingvo, sed en grandskala evoluigo aŭ kompleksa logiko, senerarigo povas fariĝi malfacila pro neintencitaj flankaj efikoj aŭ variabla reuzo. PureSharp naskiĝis por provizi al programistoj "liberecon (de cimoj)" en formo de "limigoj".

---
## Permesilo
Ĉi tiu projekto estas publikigita sub la MIT-permesilo.
