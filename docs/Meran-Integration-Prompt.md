# Prompt d’intégration — API Meran (consommateur : Kalon.Back)

Copie-colle ce bloc dans ton projet Meran (issue, spec, ou assistant) pour aligner l’implémentation côté Meran avec ce que consomme **Kalon.Back**.

---

## Contexte

**Kalon.Back** est l’API produit (auth locale, données métier). **Meran** est l’API meta de gestion des utilisateurs : certification, statut, abonnement/plan, et à terme création d’utilisateurs et réception d’événements métier (Stripe/PSP) orchestrés par Kalon.

Kalon doit appeler Meran en **machine-to-machine** avec un JWT qui satisfait `[Authorize(Roles = "ApiClient")]`.

## Ce que Kalon fait aujourd’hui

1. **Configuration**
   - `Application:ApplicationId` (GUID) : identifie l’application métier côté Meran (multi-tenant dans l’URL).
   - `MeranOptions:BaseUrl` : URL de base de l’API Meran (sans slash final).
   - Auth vers Meran, dans l’ordre de priorité :
     - **OAuth2 client credentials** si `TokenEndpoint` + `ClientId` + `ClientSecret` sont renseignés (recommandé en prod).
     - Sinon **token Bearer statique** dans `ApiClientToken` (JWT ou valeur brute ; Kalon ajoute le préfixe `Bearer` si besoin).

2. **Appel HTTP**
   - `GET {BaseUrl}/api/applications/{applicationId}/users/{userId}/status`
   - `Authorization: Bearer <access_token>`
   - `applicationId` = `Application:ApplicationId`
   - `userId` = `MeranId` stocké sur l’utilisateur Kalon (pas l’`Id` interne Kalon).

3. **Endpoint proxy côté Kalon (pour le front ou les tests internes)**
   - `GET /api/meran/users/{kalonUserId}/status` : résout l’utilisateur Kalon, lit `MeranId`, appelle Meran avec `Application:ApplicationId`.

## Ce que Meran doit fournir (contrat minimal)

### Authentification M2M

- Un mécanisme pour que Kalon obtienne un **access_token** JWT avec le rôle **`ApiClient`** (ou équivalent reconnu par `Roles = "ApiClient"`).
- Options acceptables :
  - **OAuth2** : endpoint token (souvent `POST` `application/x-www-form-urlencoded` avec `grant_type=client_credentials`, `client_id`, `client_secret`, `scope` optionnel), réponse JSON avec au minimum `access_token` et de préférence `expires_in` (secondes).
  - **Phase bootstrap** : émission manuelle d’un JWT longue durée pour `ApiClientToken` (moins idéal mais acceptable pour démarrer).

### Endpoint statut utilisateur

- Route alignée avec ce que Kalon appelle :
  - `GET /api/applications/{applicationId:guid}/users/{userId:guid}/status`
- Attributs attendus côté Meran (exemple) :
  - `[Authorize(Roles = "ApiClient")]`
- Réponse JSON : structure libre côté Meran ; Kalon la renvoie telle quelle (ex. `isActive`, `plan`, etc.) tant que c’est du JSON valide.

### Évolutions futures (hors scope immédiat mais à prévoir dans la conception Meran)

- Création / provisioning d’utilisateurs depuis Kalon (tunnel de vente).
- Webhooks Stripe/PSP reçus par Kalon puis synchronisation vers Meran (idempotence, traçabilité).
- Pas besoin d’une instance Meran par application métier : **`applicationId` dans l’URL** suffit pour le multi-tenant ; le **client OAuth** Kalon est une identité technique unique (pas un déploiement par tenant).

## Contraintes de sécurité

- HTTPS en production ; secrets (`ClientSecret`, tokens) hors dépôt (User Secrets, variables d’environnement, Key Vault).
- JWT avec durée de vie raisonnable ; rotation des `client_secret` possible sans changer le modèle Kalon si OAuth2 est utilisé.

## Résumé pour l’implémenteur Meran

1. Exposer ou brancher un **token endpoint** client credentials + client `kalon-backend` avec rôle **`ApiClient`** dans le JWT.
2. Garantir que `GET .../applications/{applicationId}/users/{userId}/status` valide le JWT et le rôle.
3. Documenter l’URL exacte du token endpoint, les champs du body, et le claim de rôle utilisé (`role` vs autre) pour que Kalon et l’IdP soient alignés.

---

Fin du prompt.
