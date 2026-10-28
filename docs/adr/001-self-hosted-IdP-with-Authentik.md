# ADR-003: Migrate IDP to Authentik

**Status:** Proposed  
**Date:** October 2026  
**Authors:** Nick Burns  
**Project:** DistributeAid Needs Assessment

---

## Context
Currently, Needs Assessment utilizes Google and Microsoft as identity providers for user authentication. In order to achieve digital sovereignty we have decided to move away from Big Tech solutions and external dependency.

An open-source, self-hostable OIDC identity provider is desired, but EU-based third-party hosting has also been considered. The provider should support Passkeys/WebAuthn as the primary sign-in method with the option for password sign-in as a back-up.

Identity providers meeting the requirements include:
- Zitadel
- Keycloak
- Authentik
- Rauthy

---
## Decision
Implement Authentik as primary identity provider.

Authentik is a robust open source identity provider that meets all requirements while being relatively easy to learn and set up. 

---
## Reasoning
### Meets Requirements
Authentik is open source and supports OIDC protocol, offers Passkey authentication and self-hosting. 

It can easily be swapped in place of the current Microsoft/Google authentication in Needs Assessment. The NextAuth.js package used in the web app already includes support for Authentik and will only require minor code changes to implement.

Users can be onboarded and managed in the admin console. Groups, roles, and permissions are all configurable and there is support for self-onboarding of new users.

### Well Documented and easy integration
Authentik has robust documentation detailing the self-hosting setup and integration into existing applications. 

For a development environment Authentik can easily be deployed with a provided Docker image, in a production environment Authentik can be hosted with Co-op Cloud.

### Can be used for other resources
Adopting Authentik will allow us to have a single source of authentication for multiple applications including some third party tools such as Baserow.

---
## Alternatives Considered

### Zitadel
Zitadel meets the same requirements as Authentik, self-hostable, supports OIDC and offers Passkey authentication. Authentik wins out by offering more supported protocols and login methods allowing for more future customization.

### Keycloak
Keycloak is a more mature option with a larger ecosystem. Provides the same options, self-hosting, OIDC, and Passkeys. Keycloak comes with a much higher learning curve and more complicated setup. Authentik better fits the needs of DA, it will be easier for new contributers to pick up.

### Rauthy
Rauthy is a lighter weight option offering OIDC and Passkey support along with the ability to self host. Authentik has been around longer and has a larger adoption base, seems to be more proven than Rauthy making it the safer choice.

---
## Consequences

### Positive
- Single identity provider for apps, can be used for Needs Assessment, Baserow, and future tools.
- Removes dependence on Google/Microsoft, freeing DA from being concerned with future policy changes or loss of access.
- Complete control over login process. MFA rules, session settings, user and group policies can all be configured in one place.
- Authentication flow configurations can be exported and stored in source control, allowing for quick, repeatable set up of environments.

### Negative
- Increases maintenance and security responsibility, uptime and patching will be fully on DA to maintain.
- While Authentik is well documented and not overtly complex, it will add an additional tool for DA staff and contributors to learn.
