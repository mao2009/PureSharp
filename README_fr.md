# PureSharp

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

[![CI & NuGet Upload](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml/badge.svg)](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml) [![NuGet](https://img.shields.io/nuget/v/loach.PureSharp.svg)](https://www.nuget.org/packages/loach.PureSharp) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![X (Twitter) Follow](https://img.shields.io/twitter/follow/loach_mao)](https://x.com/loach_mao)

**PureSharp** est un ensemble d'outils conçu pour soutenir fermement la « transparence référentielle » et l'« immutabilité » en C#, apportant la sécurité de la programmation fonctionnelle à C#. Il utilise des analyseurs Roslyn pour imposer une écriture de code robuste et résistante aux bogues au niveau de la compilation.

## Concepts Clés

1.  **Application de la Pureté :** Déclarer explicitement la logique sans effets secondaires et la vérifier mécaniquement.
2.  **Introduction de l'Immutabilité :** Réaliser des « variables locales non réassignables », une fonctionnalité absente de la spécification du langage, grâce à des conventions de nommage intuitives.
3.  **Flux de Contrôle Sécurisé :** Fournir des branchements conditionnels de style pipeline pour prévenir les exceptions au moment de l'exécution et les oublis.

## Caractéristiques Principales

### 1. PureSharp.Core
Fournit des attributs fondamentaux et des utilitaires.

*   **Attribut `[PureMethod]`** : Déclare une méthode comme « pure » (référentiellement transparente).
*   **`Fluent.If`** : Une interface fluide qui permet d'écrire des instructions `if-else` sous forme d'expressions.

### 2. PureSharp.Analyzers (Analyseurs Roslyn)
Surveille l'écriture du code en temps réel et signale les violations de règles.

#### Vérification de la Transparence Référentielle (RTxxxx)
Une « Erreur » est signalée lorsqu'une méthode annotée avec `[PureMethod]` effectue les opérations suivantes :
*   Accès à des champs statiques et mutables (non readonly).
*   Appel de méthodes non pures (méthodes sans `[PureMethod]`).
*   Opérations d'E/S (accès à la Console, Fichier, Réseau, etc.).

#### Application de l'Immutabilité des Variables Locales (LVPxxxx)
Réalise l'immutabilité en utilisant une convention de nommage qui commence par un souligné (`_`).
*   **Immutabilité Obligatoire** : Interdit la réassignation aux variables locales commençant par `_` (Erreur).
*   **Initialisation Obligatoire** : Les variables commençant par `_` doivent être initialisées au moment de la déclaration (Erreur).
*   **Suggestion de Nommage** : Suggère d'ajouter un souligné (`_`) aux variables qui n'ont jamais été réassignées (Avertissement).

#### Vérification de Terminaison de FluentIf (FIFxxxx)
*   Vérifie que les chaînes commençant par `Fluent.If` sont correctement terminées par `.Else()`. L'absence de terminaison entraînera une erreur de compilation.

---

## Démarrage Rapide

### 1. Écriture de Méthodes Référentiellement Transparentes
```csharp
using PureSharp.Core;

public class Calculator
{
    private static int _globalCache; // Champ statique mutable

    [PureMethod]
    public int Add(int a, int b)
    {
        // OK : Calcul uniquement
        return a + b; 
        
        // NG : L'accès à un champ statique provoque l'erreur RT0001
        // _globalCache = a + b; 
        
        // NG : Les opérations d'E/S provoquent l'erreur RT0003
        // Console.WriteLine(a); 
    }
}
```

### 2. Utilisation de Variables Locales Immuables
```csharp
public void Process()
{
    int _result = Calculate(); // Déclarée comme une variable immuable
    
    // NG : La tentative de réassignation provoque l'erreur LVP0001
    // _result = 10; 
    
    int count = 0; // Variable régulière
    // Suggestion : Si elle n'est pas réassignée, un avertissement pour changer en « _count » (LVP0003)
}
```

### 3. Branchement Conditionnel Sécurisé (FluentIf)
```csharp
int status = Fluent.If(score >= 80, () => 1)
                   .ElseIf(score >= 60, () => 2)
                   .Else(0); // L'oubli de .Else() provoque l'erreur FIF0001
```

---

## Structure du Projet

*   **PureSharp.Core** : Fournit les attributs de base et les bibliothèques d'exécution (.NET 10.0 / netstandard2.0).
*   **PureSharp.Analyzers** : L'analyseur Roslyn principal (netstandard2.0).
*   **PureSharp.Analyzers.Tests** : Tests unitaires pour vérifier le comportement de l'analyseur (xUnit).

---

## Motivation pour le Développement
C# est un langage très puissant, mais dans les développements à grande échelle ou les logiques complexes, le débogage peut devenir difficile en raison d'effets secondaires involontaires ou de la réutilisation de variables. PureSharp est né pour offrir aux développeurs la « liberté (contre les bogues) » sous forme de « contraintes ».

---
## Licence
Ce projet est publié sous la licence MIT.
