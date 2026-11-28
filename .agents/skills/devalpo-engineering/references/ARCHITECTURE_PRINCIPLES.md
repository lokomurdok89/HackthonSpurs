# Architecture Principles

Prefer the simplest architecture that makes responsibilities, dependencies and
operating behavior understandable.

- Separate delivery, application behavior, domain rules, persistence and
  external integration where that separation reduces coupling.
- Keep dependencies directed toward stable business and contract abstractions.
- Keep endpoints, controllers and handlers small; delegate orchestration rather
  than accumulating unrelated concerns.
- Keep application behavior independent of infrastructure details when external
  systems can change or need testing in isolation.
- Isolate external integrations behind explicit boundaries and map their models
  before they reach public contracts.
- Make public contracts explicit, compatible by default and incrementally
  evolvable.
- Prefer cohesive modules and clear ownership over large classes or broad
  utility layers.
- Externalize environment-specific configuration and avoid abstractions that do
  not remove real complexity.
- Record decisions with long-term contract, persistence, security, deployment
  or operating consequences as ADRs.

These principles do not prescribe a universal architecture or require CQRS,
MediatR, repositories, microservices, DDD or any specific framework.
