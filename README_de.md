# PureSharp

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

**PureSharp** ist ein Toolset, das entwickelt wurde, um die „referenzielle Transparenz“ und „Immutabilität“ (Unveränderlichkeit) in C# stark zu unterstützen und die Sicherheit der funktionalen Programmierung in C# zu bringen. Es nutzt Roslyn-Analyzer, um robustes, fehlerresistentes Schreiben von Code auf Kompilierungsebene zu erzwingen.

## Kernkonzepte

1.  **Erzwingung von Reinheit (Purity):** Logik ohne Seiteneffekte explizit deklarieren und mechanisch verifizieren.
2.  **Einführung von Immutabilität:** Erreichen von „nicht neu zuweisbaren lokalen Variablen“, einer Funktion, die in der Sprachspezifikation fehlt, durch intuitive Namenskonventionen.
3.  **Sicherer Kontrollfluss:** Bereitstellung von Pipeline-artigen bedingten Verzweigungen, um Laufzeitausnahmen und Versäumnisse zu verhindern.

## Hauptmerkmale

### 1. PureSharp.Core
Bietet grundlegende Attribute und Dienstprogramme.

*   **`[PureMethod]`-Attribut**: Deklariert eine Methode als „rein“ (referenziell transparent).
*   **`Fluent.If`**: Eine flüssige Schnittstelle, die es ermöglicht, `if-else`-Anweisungen als Ausdrücke zu schreiben.

### 2. PureSharp.Analyzers (Roslyn-Analyzer)
Überwacht das Schreiben von Code in Echtzeit und meldet Regelverstöße.

#### Überprüfung der referenziellen Transparenz (RTxxxx)
Ein „Fehler“ (Error) wird gemeldet, wenn eine mit `[PureMethod]` annotierte Methode die folgenden Operationen ausführt:
*   Zugriff auf statische, veränderliche (nicht schreibgeschützte) Felder.
*   Aufruf von nicht-reinen Methoden (Methoden ohne `[PureMethod]`).
*   E/A-Operationen (Zugriff auf Konsole, Dateien, Netzwerk usw.).

#### Erzwingung der Immutabilität lokaler Variablen (LVPxxxx)
Erreicht Immutabilität durch eine Namenskonvention, die mit einem Unterstrich (`_`) beginnt.
*   **Zwingende Immutabilität**: Verbietet die Neuzuweisung zu lokalen Variablen, die mit `_` beginnen (Fehler).
*   **Zwingende Initialisierung**: Variablen, die mit `_` beginnen, müssen zum Zeitpunkt der Deklaration initialisiert werden (Fehler).
*   **Namensvorschlag**: Schlägt vor, Variablen, die nie neu zugewiesen wurden, einen Unterstrich (`_`) voranzustellen (Warnung).

#### Überprüfung des Abschlusses von FluentIf (FIFxxxx)
*   Verifiziert, dass Ketten, die mit `Fluent.If` beginnen, korrekt mit `.Else()` abgeschlossen werden. Ein fehlender Abschluss führt zu einem Kompilierfehler.

---

## Schnellstart

### 1. Schreiben von referenziell transparenten Methoden
```csharp
using PureSharp.Core;

public class Calculator
{
    private static int _globalCache; // Veränderliches statisches Feld

    [PureMethod]
    public int Add(int a, int b)
    {
        // OK: Nur Berechnung
        return a + b; 
        
        // NG: Zugriff auf statisches Feld verursacht RT0001-Fehler
        // _globalCache = a + b; 
        
        // NG: E/A-Operationen verursachen RT0003-Fehler
        // Console.WriteLine(a); 
    }
}
```

### 2. Nutzung von immutablen lokalen Variablen
```csharp
public void Process()
{
    int _result = Calculate(); // Als immutable Variable deklariert
    
    // NG: Versuch der Neuzuweisung verursacht LVP0001-Fehler
    // _result = 10; 
    
    int count = 0; // Reguläre Variable
    // Vorschlag: Wenn nicht neu zugewiesen, eine Warnung zur Änderung in „_count“ (LVP0003)
}
```

### 3. Sichere bedingte Verzweigung (FluentIf)
```csharp
int status = Fluent.If(score >= 80, () => 1)
                   .ElseIf(score >= 60, () => 2)
                   .Else(0); // Das Vergessen von .Else() verursacht den Fehler FIF0001
```

---

## Projektstruktur

*   **PureSharp.Core**: Bietet Kernattribute und Laufzeitbibliotheken (.NET 10.0 / netstandard2.0).
*   **PureSharp.Analyzers**: Der Haupt-Roslyn-Analyzer (netstandard2.0).
*   **PureSharp.Analyzers.Tests**: Unit-Tests zur Überprüfung des Verhaltens des Analyzers (xUnit).

---

## Motivation für die Entwicklung
C# ist eine sehr leistungsfähige Sprache, aber in der großflächigen Entwicklung oder bei komplexer Logik kann das Debugging aufgrund unbeabsichtigter Seiteneffekte oder der Wiederverwendung von Variablen schwierig werden. PureSharp wurde ins Leben gerufen, um Entwicklern „Freiheit (von Bugs)“ in Form von „Einschränkungen“ zu bieten.

---
## Lizenz
Dieses Projekt wird unter der MIT-Lizenz veröffentlicht.
