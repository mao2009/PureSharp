# PureSharp

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

[![CI & NuGet Upload](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml/badge.svg)](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml) [![NuGet](https://img.shields.io/nuget/v/loach.PureSharp.svg)](https://www.nuget.org/packages/loach.PureSharp) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![X (Twitter) Follow](https://img.shields.io/twitter/follow/loach_mao)](https://x.com/loach_mao)

**PureSharp** es un conjunto de herramientas diseñado para apoyar firmemente la "transparencia referencial" e "inmutabilidad" en C#, trayendo la seguridad de la programación funcional a C#. Utiliza analizadores Roslyn para imponer una escritura de código robusta y resistente a errores a nivel de compilación.

## Conceptos Básicos

1.  **Imposición de Pureza:** Declara explícitamente la lógica sin efectos secundarios y la verifica mecánicamente.
2.  **Introducción de Inmutabilidad:** Logra "variables locales no reasignables", una característica ausente en la especificación del lenguaje, a través de convenciones de nomenclatura intuitivas.
3.  **Flujo de Control Seguro:** Proporciona ramificación condicional de estilo pipeline para prevenir excepciones en tiempo de ejecución y descuidos.

## Características Principales

### 1. PureSharp.Core
Proporciona atributos fundamentales y utilidades.

*   **Atributo `[PureMethod]`**: Declara un método como "puro" (referencialmente transparente).
*   **`Fluent.If`**: Una interfaz fluida que permite escribir sentencias `if-else` como expresiones.

### 2. PureSharp.Analyzers (Analizadores Roslyn)
Monitorea la escritura de código en tiempo real e informa violaciones de reglas.

#### Verificación de Transparencia Referencial (RTxxxx)
Se informa un "Error" cuando un método anotado con `[PureMethod]` realiza las siguientes operaciones:
*   Acceso a campos estáticos y mutables (no readonly).
*   Llamada a métodos no puros (métodos sin `[PureMethod]`).
*   Operaciones de E/S (acceso a Consola, Archivos, Red, etc.).

#### Imposición de Inmutabilidad de Variables Locales (LVPxxxx)
Logra la inmutabilidad utilizando una convención de nomenclatura que comienza con un guion bajo (`_`).
*   **Inmutabilidad Obligatoria**: Prohíbe la reasignación a variables locales que comienzan con `_` (Error).
*   **Inicialización Obligatoria**: Las variables que comienzan con `_` deben inicializarse en el momento de la declaración (Error).
*   **Sugerencia de Nomenclatura**: Sugiere añadir un guion bajo (`_`) a las variables que nunca han sido reasignadas (Advertencia).

#### Verificación de Terminación de FluentIf (FIFxxxx)
*   Verifica que las cadenas que comienzan con `Fluent.If` terminen correctamente con `.Else()`. No terminar resultará en un error de compilación.

---

## Inicio Rápido

### 1. Escritura de Métodos Referencialmente Transparentes
```csharp
using PureSharp.Core;

public class Calculator
{
    private static int _globalCache; // Campo estático mutable

    [PureMethod]
    public int Add(int a, int b)
    {
        // OK: Solo cálculo
        return a + b; 
        
        // NG: Acceder a un campo estático causa el error RT0001
        // _globalCache = a + b; 
        
        // NG: Las operaciones de E/S causan el error RT0003
        // Console.WriteLine(a); 
    }
}
```

### 2. Utilización de Variables Locales Inmutables
```csharp
public void Process()
{
    int _result = Calculate(); // Declarada como una variable inmutable
    
    // NG: Intentar reasignar causa el error LVP0001
    // _result = 10; 
    
    int count = 0; // Variable regular
    // Sugerencia: Si no se reasigna, una advertencia para cambiar a "_count" (LVP0003)
}
```

### 3. Ramificación Condicional Segura (FluentIf)
```csharp
int status = Fluent.If(score >= 80, () => 1)
                   .ElseIf(score >= 60, () => 2)
                   .Else(0); // Olvidar .Else() causa el error FIF0001
```

---

## Estructura del Proyecto

*   **PureSharp.Core**: Proporciona atributos básicos y librerías de tiempo de ejecución (.NET 10.0 / netstandard2.0).
*   **PureSharp.Analyzers**: El analizador Roslyn principal (netstandard2.0).
*   **PureSharp.Analyzers.Tests**: Pruebas unitarias para verificar el comportamiento del analizador (xUnit).

---

## Motivación para el Desarrollo
C# es un lenguaje muy potente, pero en desarrollos a gran escala o lógica compleja, la depuración puede volverse difícil debido a efectos secundarios no deseados o la reutilización de variables. PureSharp nació para proporcionar a los desarrolladores "libertad (de errores)" en forma de "restricciones".

---
## Licencia
Este proyecto se lanza bajo la Licencia MIT.
