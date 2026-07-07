Súhlasím s ďalším postupom, ale pokračujme v novom chate.

Priprav jednotný projektový handoff podľa aktuálneho stavu.

Výstup daj celý do jedného jediného copy bloku.
Nepoužívaj vnorené markdown code fences.
Nevytváraj nový súbor, checkpoint, commit ani inú zmenu; priprav iba text na vloženie do nového chatu.
Príkazy, ak sú nevyhnutné, uvádzaj ako plain text pod vhodným nadpisom, napríklad „Na LXC DEV spusti:“, „Na serveri spusti:“ alebo „Vo Windows PowerShell spusti:“.
Nepis secrets, tokeny, heslá, privátne kľúče ani iné citlivé hodnoty.

Použi iba overené fakty z:

* aktuálneho chatu,
* priložených súborov a obrázkov,
* live Git alebo systémových výstupov,
* AGENTS.md, ak existuje,
* docs/CURRENT_STATE.md, ak existuje,
* potvrdených checkpointov,
* relevantných logdir výstupov,
* vykonanej technickej a browser validácie.

Nič nedomýšľaj. Neoverené, chýbajúce alebo časovo premenlivé údaje označ ako `unknown / verify live`.

Handoff musí začínať vetou:

Before doing any work, read and follow AGENTS.md and docs/CURRENT_STATE.md if they exist, then verify the live project state before making changes.

Ak AGENTS.md alebo docs/CURRENT_STATE.md neexistujú, uveď to explicitne. docs/CURRENT_STATE.md je checkpoint memory a nikdy nenahrádza live Git alebo systémový audit.

Použi jednotnú štruktúru:

# HANDOFF — [NÁZOV PROJEKTU]

## 1. Project identity

Uveď:

* názov projektu;
* repository root, server, LXC, VM alebo inú relevantnú lokalitu;
* prostredie, napríklad DEV, staging alebo production;
* používateľa, pod ktorým sa bežne pracuje;
* či projekt používa Git;
* branch, HEAD, subject a version, ak sú použiteľné;
* working-tree stav, dirty count, staged, unstaged a untracked stav;
* ahead/behind voči relevantnému upstreamu;
* pri non-Git projekte príslušnú live identitu služby, konfigurácie alebo checkpointu.

Nepoužiteľné položky označ `N/A`.

## 2. Authoritative state sources

Uveď:

* AGENTS.md a jeho úlohu, ak existuje;
* docs/CURRENT_STATE.md a jeho úlohu, ak existuje;
* posledný checkpoint;
* posledný relevantný logdir;
* ďalšie autoritatívne konfiguračné alebo stavové súbory;
* ktoré údaje sa musia po otvorení nového chatu overiť live.

Neopakuj celé stabilné pravidlá, ktoré sú už uložené v AGENTS.md.

## 3. Long-term project context

Stručne uveď:

* účel projektu;
* cieľovú architektúru alebo prevádzkový účel;
* dlhodobé požiadavky, ktoré ovplyvňujú ďalšiu prácu;
* dôležité hranice medzi DEV, staging a production.

Použi iba informácie potrebné na správne pokračovanie.

## 4. Current accepted baseline

Uveď:

* posledný accepted baseline;
* commit, checkpoint, verziu alebo file identity;
* aktuálne relevantné lines + sha256;
* ktoré služby, procesy alebo porty boli v akceptovanom stave;
* čo bolo potvrdené používateľom;
* čo bolo potvrdené technickou validáciou.

## 5. Latest completed slices

Pre každý posledný relevantný slice uveď:

* názov a rozsah;
* čo sa zmenilo;
* čo sa nezmenilo;
* výsledok validácie;
* commit, checkpoint alebo logdir;
* či je slice closed/accepted.

Nevypisuj celú históriu projektu. Uveď iba deltu potrebnú na pokračovanie.

## 6. Validation status

Rozdeľ stav na:

* closed/accepted;
* audit-only;
* pending;
* unknown / verify live.

Uveď vykonané kontroly, napríklad:

* lint;
* typecheck;
* build;
* testy;
* HTTP smoke;
* browser validation;
* service status;
* config validation;
* DNS kontrolu;
* DB safety kontrolu;
* file identity;
* diff check.

Uvádzaj iba skutočne vykonané validácie.

## 7. Current scope

Uveď presne posledný schválený scope.

Nevytváraj nový plán mimo tohto scope.
Nespájaj viac nezávislých zmien do jedného kroku.
Ak je ďalší krok iba audit, nesmie sa meniť konfigurácia, kód, databáza ani runtime.

## 8. Next action

Uveď presne jeden bezprostredný ďalší krok.

Musí obsahovať:

* či ide o read-only audit alebo write slice;
* ktoré súbory, služby alebo časti projektu sa majú preveriť;
* čo sa v tomto kroku nesmie meniť;
* aký výstup má používateľ poslať späť;
* aké guard podmienky sa musia overiť pred pokračovaním.

Neuvádzaj alternatívne plány.

## 9. Known risks and regression prevention

Uveď iba relevantné:

* predchádzajúce chyby alebo regresie;
* ich príčinu, ak je overená;
* ako zabrániť ich opakovaniu;
* procesy alebo súbory, ktoré sa nesmú slepo prepísať;
* zásahy, ktoré môžu byť prepísané aktualizáciou;
* požiadavky na browser alebo runtime validáciu.

## 10. Hard restrictions

Uveď spoločné zákazy:

* žiadne secrets v handoffe;
* žiadny push, pull request, merge, tag, release alebo deployment bez explicitného pokynu;
* žiadne rozšírenie scope bez súhlasu;
* žiadne zmeny mimo určeného projektu;
* žiadne kopírovanie projektovo špecifického stavu alebo konfigurácie z iného projektu bez schválenia;
* žiadne produkčné, DNS, DB, autentifikačné, billingové, sieťové alebo infraštruktúrne zmeny, ak nie sú explicitne súčasťou schváleného scope;
* žiadne preskočenie fresh read-only auditu;
* žiadne serverové command bloky obsahujúce `set -e`, `set -euo pipefail`, explicitný `exit` alebo inú konštrukciu, ktorá môže ukončiť SSH/session pri chybe.

Pridaj aj projektovo špecifické zákazy potvrdené v aktuálnom projekte.

## 11. Continuation procedure

Uveď stručný bezpečný postup:

1. načítať projektové pravidlá a checkpoint memory;
2. vykonať fresh live read-only audit;
3. porovnať live stav s handoffom;
4. pri rozdiele zastaviť write postup a označiť rozdiel;
5. vykonať iba jediný schválený next action;
6. po každom slice vykonať technickú validáciu;
7. browser alebo používateľskú validáciu vyžadovať tam, kde je relevantná;
8. commit, checkpoint, push alebo deployment vykonať iba podľa explicitného schválenia.

Na konci uveď:

* `Current status:`
* `Next action:`
* `Do not proceed beyond this action without reviewing its output.`
