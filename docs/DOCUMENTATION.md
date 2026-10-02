# Documentation and translation policy

PureSharp has multiple README translations, but behavioral contracts must have one authoritative source so translations cannot silently redefine analyzer behavior.

## Canonical sources

The documentation hierarchy for v1 is:

1. [`DIAGNOSTICS.md`](DIAGNOSTICS.md) is the SSOT for diagnostic IDs, categories, default severities, enabled state, compatibility policy, and rule intent.
2. Specialized contract documents define detailed behavior:
   - [`PURITY-SEMANTICS.md`](PURITY-SEMANTICS.md)
   - [`CALL-CONTRACT.md`](CALL-CONTRACT.md)
   - [`LVP.md`](LVP.md)
   - [`FLUENT_IF.md`](FLUENT_IF.md)
   - [`COMPATIBILITY.md`](COMPATIBILITY.md)
3. `README.md` is the canonical product overview and installation entry point. It summarizes, but does not override, the contract documents.
4. `README_ja.md` and the other translated READMEs are informational translations. If a translation conflicts with the canonical English/contract documentation, the canonical contract wins.

The NuGet package embeds `README.md`, so the English README must always contain the current installation path and links to normative documentation.

## Translation maintenance

A change to diagnostic semantics, ID, category, or default severity must update the canonical contract and `README.md` in the same change. `README_ja.md` should be updated for user-facing v1 guidance at the same time when practical.

Other translations are best-effort. They may temporarily lag wording changes, but they must not claim a different diagnostic identity or severity. A translation-only wording fix is non-breaking.

## Examples

Consumer commands and code examples that are intended as release gates live in canonical English documentation and CI scripts. Translations may reproduce those examples, but CI does not independently execute every translated snippet.

The authoritative clean-project flow is [`GETTING_STARTED.md`](GETTING_STARTED.md); all seven rule examples are in [`RULE-EXAMPLES.md`](RULE-EXAMPLES.md).

## Contribution rule

Do not add a new behavioral guarantee only to a README translation. Update the appropriate contract document first, then synchronize summaries and translations.
