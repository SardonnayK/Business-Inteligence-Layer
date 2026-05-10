namespace Orchestrator.Infrastructure.Seeding;

internal static class SeedData
{
    // ─── FibreCore Networks (Company A) ───────────────────────────────────────
    // Incumbent · 75% market share · methodical · premium-priced · urban-first

    internal static class FibreCore
    {
        internal static readonly Guid TenantId = new("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");

        internal static class Projects
        {
            internal static readonly Guid UrbanRollout    = new("a1a1a1a1-0000-0000-0000-000000000010");
            internal static readonly Guid EnterpriseSales = new("a1a1a1a1-0000-0000-0000-000000000020");
            internal static readonly Guid BackboneUpgrade = new("a1a1a1a1-0000-0000-0000-000000000030");
        }

        internal static readonly ProjectSeed[] ProjectSeeds =
        [
            new(Projects.UrbanRollout, TenantId,
                "Urban FTTP Rollout",
                "Phased FTTP deployment across the top 8 metropolitan areas, prioritising high-density residential zones with the strongest projected ARPU and lowest cost-per-premise passed."),
            new(Projects.EnterpriseSales, TenantId,
                "Enterprise Fibre Sales Programme",
                "Go-to-market motion targeting large enterprise and government accounts for dedicated fibre, SLA-backed tiers, and managed network services."),
            new(Projects.BackboneUpgrade, TenantId,
                "National Backbone Capacity Upgrade",
                "Upgrade of core and aggregation nodes to 10G/25G symmetric throughput with redundant ring topology across inter-city routes."),
        ];

        internal static readonly RequirementSeed[] RequirementSeeds =
        [
            // Urban Rollout
            new(new("a1000001-0000-0000-0000-000000000000"), Projects.UrbanRollout,
                "The rollout sequencing tool must rank zones by a composite score weighting premises density, median household income, and existing broadband penetration. Zones below the 60th percentile are deferred to Phase 2.", "Approved"),
            new(new("a1000002-0000-0000-0000-000000000000"), Projects.UrbanRollout,
                "All civil works contracts must include a unit-cost cap approved by the Capital Expenditure Committee. No contract exceeding $2 million may be signed without CFO countersignature.", "Approved"),
            new(new("a1000003-0000-0000-0000-000000000000"), Projects.UrbanRollout,
                "The customer activation portal must enforce a 30-day minimum lead time between premises-passed notification and first available installation slot.", "In Review"),
            new(new("a1000004-0000-0000-0000-000000000000"), Projects.UrbanRollout,
                "Suburban and rural zones must not appear in any public coverage commitments until internal sign-off confirms a build start date within 18 months.", "Approved"),
            new(new("a1000005-0000-0000-0000-000000000000"), Projects.UrbanRollout,
                "The field operations system must capture actual cost-per-premise at the sub-zone level. Any zone exceeding budget by more than 12% triggers a mandatory scope review.", "Draft"),

            // Enterprise Sales
            new(new("a2000001-0000-0000-0000-000000000000"), Projects.EnterpriseSales,
                "The CRM must support tiered enterprise account classification (Strategic, Key, Commercial). Any discount exceeding 15% off the rate card requires Pricing Committee approval.", "Approved"),
            new(new("a2000002-0000-0000-0000-000000000000"), Projects.EnterpriseSales,
                "All enterprise contracts must include a minimum 36-month committed term for dedicated fibre circuits. Contract templates must be reviewed by Legal before any variation is presented.", "Approved"),
            new(new("a2000003-0000-0000-0000-000000000000"), Projects.EnterpriseSales,
                "The SLA management system must monitor enterprise circuit uptime in real time and auto-generate a credit when availability falls below the contracted 99.95% threshold.", "In Review"),
            new(new("a2000004-0000-0000-0000-000000000000"), Projects.EnterpriseSales,
                "Sales representatives must not quote installation lead times shorter than 45 business days for new enterprise sites. The order system must enforce this and prevent override without Network Operations sign-off.", "Approved"),

            // Backbone Upgrade
            new(new("a3000001-0000-0000-0000-000000000000"), Projects.BackboneUpgrade,
                "All backbone node upgrades must follow a change-freeze calendar prohibiting planned maintenance during the first and last five business days of each month.", "Approved"),
            new(new("a3000002-0000-0000-0000-000000000000"), Projects.BackboneUpgrade,
                "Every inter-city route must maintain minimum N+1 redundancy before any FTTP aggregation traffic is migrated. The Network Architecture team must produce a signed redundancy attestation prior to cutover.", "Approved"),
            new(new("a3000003-0000-0000-0000-000000000000"), Projects.BackboneUpgrade,
                "Vendor selection for core routing hardware is restricted to the two suppliers on the approved vendor register. Any new vendor requires a Security and Interoperability Review Board assessment with a 90-day lead time.", "In Review"),
            new(new("a3000004-0000-0000-0000-000000000000"), Projects.BackboneUpgrade,
                "Network monitoring tooling must expose 10G/25G interface utilisation metrics to the NOC dashboard before any upgraded node carries live customer traffic.", "Draft"),
        ];

        internal static readonly ContextChunkSeed[] ContextChunks =
        [
            new("FibreCore's pricing strategy for residential FTTP is anchored to premium positioning. Our entry-tier plan is priced at a deliberate premium over legacy ADSL and cable equivalents, reflecting the superior quality of our network. We do not intend to compete on price with low-cost operators; our brand equity and network reputation are the primary value drivers.",
                "FibreCore Internal Strategy — Residential Pricing Policy v3.2", "pricing_strategy"),

            new("Enterprise fibre pricing follows a published rate card reviewed annually by the Pricing Committee. Custom pricing is available only for Strategic and Key accounts and requires dual approval. FibreCore does not engage in speculative discounting to win volume; margin protection is a standing commercial objective.",
                "FibreCore Enterprise Commercial Policy v2.1", "pricing_strategy"),

            new("Our primary residential target demographic is dual-income households in metropolitan areas with children under 18. These households exhibit the highest willingness to pay for reliable high-speed connectivity, the lowest churn risk, and the strongest upsell potential for bundled streaming and security products.",
                "FibreCore Market Segmentation Report 2025", "target_demographics"),

            new("FibreCore's enterprise target market is organisations with 50 or more employees in fixed commercial premises within our metropolitan footprint. We prioritise financial services, legal, healthcare, and government sectors, which have the highest SLA sensitivity and the least price elasticity.",
                "FibreCore Enterprise Sales Strategy 2025–2027", "target_demographics"),

            new("Our FTTP rollout is structured across four phases. Phase 1 covers the top 8 metropolitan areas and targets completion by Q4 2026. Phase 2 extends to secondary cities by Q2 2028. Suburban and rural areas fall in Phases 3 and 4 respectively, with no committed external completion dates published at this time.",
                "FibreCore National FTTP Rollout Master Plan v1.4", "rollout_phasing"),

            new("Within Phase 1 metropolitan deployments, sub-zone sequencing is determined by a scoring model factoring premises density, projected take-up rate, infrastructure reuse from existing duct assets, and estimated ARPU. High-scoring zones receive build priority regardless of geographic proximity to previously completed zones.",
                "FibreCore Urban Deployment Prioritisation Framework", "rollout_phasing"),

            new("FibreCore does not make public coverage commitments for areas where civil works have not commenced. Internal build forecasts are commercially sensitive and are not shared with third parties unless required under a formal regulatory disclosure obligation. This policy prevents premature demand signalling to competitors.",
                "FibreCore Communications and Disclosure Policy v4.0", "rollout_phasing"),

            new("Our standard residential SLA guarantees network availability of 99.9% measured monthly at the access node. Fault restoration targets are four hours for total loss of service and 24 hours for degraded service. Compensation for SLA breaches is processed as a bill credit in the following billing cycle without requiring a customer claim.",
                "FibreCore Residential Service Level Agreement — Standard Terms", "sla_commitments"),

            new("Enterprise customers on dedicated fibre circuits are eligible for our Platinum SLA, guaranteeing 99.95% monthly availability and a two-hour restoration target. SLA credits are calculated at 10% of monthly recurring charges per hour of downtime beyond the restoration target, capped at 30% of the monthly invoice.",
                "FibreCore Enterprise SLA Schedule — Platinum Tier v2.3", "sla_commitments"),

            new("FibreCore's competitive response policy is measured and deliberate. We do not engage in reactive price matching against challenger operators. When a competitor enters our market, the standard response is to reinforce our service quality narrative through targeted retention campaigns and loyalty incentives for at-risk customers.",
                "FibreCore Competitive Response Playbook v1.1", "competitive_response"),

            new("Where a new entrant demonstrates sustained market share gain of more than 5 percentage points over two consecutive quarters, a Competitive Threat Review is escalated to the Executive Committee. The Committee may authorise a localised promotional pricing programme, but such programmes must carry a defined sunset date.",
                "FibreCore Competitive Response Playbook v1.1", "competitive_response"),

            new("FibreCore's partnership approach is selective and structured. We partner with national retailers for consumer acquisition, preferred system integrators for enterprise managed services, and a small panel of civil contractors for network build. All partners must be accredited annually and meet minimum performance thresholds.",
                "FibreCore Partner and Channel Policy v3.0", "partnership_approach"),

            new("Our regulatory compliance stance is one of proactive engagement. FibreCore participates in all mandated open-access and structural separation consultations and meets all wholesale reference offer obligations. We comply fully with all obligations but do not volunteer concessions beyond what is required.",
                "FibreCore Regulatory Affairs Policy and Governance Framework", "regulatory_compliance"),

            new("All network infrastructure deployed under the FTTP programme is subject to the national Critical Infrastructure Protection framework. FibreCore conducts annual penetration testing of OLT and aggregation systems, and all vendors with access to network management interfaces must meet our Supplier Security Standard.",
                "FibreCore Network Security and Compliance Policy v2.2", "regulatory_compliance"),

            new("FibreCore's data retention and privacy practices are governed by the national Data Protection Act. Customer usage data is retained for a maximum of 24 months for billing and fault resolution. We do not sell anonymised usage data to third parties. All data processing agreements with technology vendors are reviewed by Legal before execution.",
                "FibreCore Data Governance and Privacy Policy v1.9", "regulatory_compliance"),
        ];
    }

    // ─── SwiftFibre (Company B) ───────────────────────────────────────────────
    // Challenger · 25% market share · aggressive · value-priced · suburban-first

    internal static class SwiftFibre
    {
        internal static readonly Guid TenantId = new("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");

        internal static class Projects
        {
            internal static readonly Guid SuburbanEdge      = new("b2b2b2b2-0000-0000-0000-000000000010");
            internal static readonly Guid ZeroCapPricing    = new("b2b2b2b2-0000-0000-0000-000000000020");
            internal static readonly Guid AcquisitionBlitz  = new("b2b2b2b2-0000-0000-0000-000000000030");
        }

        internal static readonly ProjectSeed[] ProjectSeeds =
        [
            new(Projects.SuburbanEdge, TenantId,
                "SuburbanEdge Rollout Programme",
                "Aggressive FTTP expansion into underserved suburban and peri-urban zones that FibreCore has deprioritised. Target: 150,000 new premises passed within 18 months, with community-level saturation before the incumbent can respond."),
            new(Projects.ZeroCapPricing, TenantId,
                "ZeroCap Disruptive Pricing Programme",
                "Design and launch a transparent, no-lock-in pricing tier that undercuts FibreCore's equivalent plans by at least 20%. Eliminate installation fees, exit fees, and mid-contract price hikes."),
            new(Projects.AcquisitionBlitz, TenantId,
                "AcquisitionBlitz Customer Growth Initiative",
                "Multi-channel acquisition campaign targeting FibreCore switchers and first-time fibre adopters. Combines hyper-local digital marketing, community ambassadors, and a same-week install guarantee to convert at scale."),
        ];

        internal static readonly RequirementSeed[] RequirementSeeds =
        [
            // SuburbanEdge
            new(new("b1000001-0000-0000-0000-000000000000"), Projects.SuburbanEdge,
                "The rollout plan must prioritise suburban postcodes where FibreCore has not filed infrastructure permits in the last 24 months, ensuring first-mover advantage in at least 80% of newly targeted zones.", "Approved"),
            new(new("b1000002-0000-0000-0000-000000000000"), Projects.SuburbanEdge,
                "End-to-end install time from permit approval to premises live must not exceed 45 days per street cluster, with a stretch target of 30 days. Any cluster exceeding 60 days triggers an automatic executive escalation.", "Approved"),
            new(new("b1000003-0000-0000-0000-000000000000"), Projects.SuburbanEdge,
                "Each new suburban zone must achieve a minimum 35% premises take-up rate within 6 months of going live, validated by active subscriber counts rather than expressions of interest.", "In Review"),
            new(new("b1000004-0000-0000-0000-000000000000"), Projects.SuburbanEdge,
                "All rural nodes in the suburban fringe expansion must support symmetric 1Gbps speeds at launch. No rural site may launch on a plan lower than 100Mbps symmetric regardless of backhaul constraints.", "Approved"),
            new(new("b1000005-0000-0000-0000-000000000000"), Projects.SuburbanEdge,
                "The rollout scheduling system must integrate with council permit APIs in all 12 target local authority areas, alerting the build operations team within 2 hours of any permit delay or rejection.", "Draft"),

            // ZeroCap Pricing
            new(new("b2000001-0000-0000-0000-000000000000"), Projects.ZeroCapPricing,
                "The ZeroCap tier must include at least three plans (Essential, Rapid, Unlimited) with no activation fee, no exit fee, and a 24-month price lock. Any price increase must require 90 days written notice.", "Approved"),
            new(new("b2000002-0000-0000-0000-000000000000"), Projects.ZeroCapPricing,
                "All published pricing must include line rental, router hardware, and installation in a single all-in monthly figure. No add-on fees may appear at checkout that were not displayed on the primary plan listing page.", "Approved"),
            new(new("b2000003-0000-0000-0000-000000000000"), Projects.ZeroCapPricing,
                "A competitive comparison tool must be built into the customer-facing website pulling live FibreCore published pricing, displaying a side-by-side equivalent plan comparison updated at least weekly.", "In Review"),
            new(new("b2000004-0000-0000-0000-000000000000"), Projects.ZeroCapPricing,
                "The billing system must support mid-month plan upgrades without pro-rata penalties and must auto-apply future price reductions to existing subscribers on equivalent plans within one billing cycle.", "Draft"),
            new(new("b2000005-0000-0000-0000-000000000000"), Projects.ZeroCapPricing,
                "SwiftFibre must publish a public SLA committing to minimum guaranteed speeds of 80% of advertised throughput at peak hours (6pm–10pm), with automatic bill credits issued if this threshold is missed.", "Approved"),

            // AcquisitionBlitz
            new(new("b3000001-0000-0000-0000-000000000000"), Projects.AcquisitionBlitz,
                "The AcquisitionBlitz campaign must deliver a same-week installation guarantee: a confirmed engineer appointment within 5 business days of contract completion. This guarantee must be published, tracked, and reported weekly to the executive team.", "Approved"),
            new(new("b3000002-0000-0000-0000-000000000000"), Projects.AcquisitionBlitz,
                "A community ambassador programme must be launched in each newly covered postcode, recruiting a minimum of 3 local advocates per 500 premises who receive referral incentives for each successful sign-up they generate.", "In Review"),
            new(new("b3000003-0000-0000-0000-000000000000"), Projects.AcquisitionBlitz,
                "The CRM must flag all prospects identified as current FibreCore subscribers and route them to a dedicated win-back offer flow with an enhanced first-3-months discount not available in the standard funnel.", "Approved"),
            new(new("b3000004-0000-0000-0000-000000000000"), Projects.AcquisitionBlitz,
                "Digital advertising spend must be geo-fenced to within 2km of active coverage boundaries. The campaign management platform must enforce this boundary and alert if any ad impressions are served outside active zones.", "Draft"),
            new(new("b3000005-0000-0000-0000-000000000000"), Projects.AcquisitionBlitz,
                "Post-install NPS surveys must trigger automatically 72 hours after a customer goes live. A target NPS of 70+ must be maintained. Any month where NPS falls below 60 triggers a root-cause review within 5 business days.", "Approved"),
        ];

        internal static readonly ContextChunkSeed[] ContextChunks =
        [
            new("SwiftFibre is committed to being the most price-transparent broadband provider in the market. Our ZeroCap pricing philosophy means every customer sees one number: a single monthly figure that includes everything. Hidden fees are the incumbent's weapon, and transparency is ours.",
                "SwiftFibre Internal Strategy Brief v2.4", "pricing_strategy"),

            new("Our plans are priced at a minimum 20% below FibreCore's equivalent tier at all times. We review competitor pricing weekly and adjust within 48 hours if parity is detected. Being cheaper is not a race to the bottom — it is a deliberate market capture strategy while we grow our subscriber base.",
                "SwiftFibre Internal Strategy Brief v2.4", "pricing_strategy"),

            new("SwiftFibre exists because the dominant incumbent got comfortable. FibreCore Networks controls 75% of the market and has spent a decade prioritising urban dense zones, raising prices mid-contract, and delivering mediocre customer service. We are the alternative that customers in left-behind communities have been waiting for.",
                "SwiftFibre Brand & Positioning Playbook", "competitive_differentiation"),

            new("Our mission is to make fast, fair fibre available to every household — not just the profitable urban postcodes. Where FibreCore sees low return on investment, we see underserved customers and untapped market share. Suburban and rural expansion is not a concession; it is our core growth engine.",
                "SwiftFibre Brand & Positioning Playbook", "competitive_differentiation"),

            new("SwiftFibre's build phases are structured in 90-day sprints, each targeting a cluster of adjacent postcodes to maximise trench reuse and local contractor efficiency. We do not cherry-pick profitable streets — we commit to full-zone coverage or we do not enter. This all-in approach builds community trust and accelerates word-of-mouth adoption.",
                "SwiftFibre Operations Handbook — Rollout Standards", "rollout_phasing"),

            new("Our SuburbanEdge programme prioritises postcodes where FibreCore has not filed new infrastructure permits in 24 months. We monitor permit registries in all 12 target local authorities in real time. Speed of entry matters: the first quality provider in a community earns loyalty that is very hard for a late-arriving incumbent to dislodge.",
                "SwiftFibre Operations Handbook — Rollout Standards", "rollout_phasing"),

            new("SwiftFibre guarantees a confirmed engineer installation appointment within 5 business days of contract completion for all customers in live coverage areas. This is not an aspiration — it is a published, tracked commitment. If we miss this window, the customer receives a full month's credit, no questions asked.",
                "SwiftFibre SLA Commitment Charter", "install_time_slas"),

            new("Our internal build target is to take any new street cluster from permit approval to first premises live in under 45 days. Our stretch goal is 30 days. We track this metric weekly and publish it on our operations dashboard so every team member can see where we stand against target.",
                "SwiftFibre SLA Commitment Charter", "install_time_slas"),

            new("Churn is the enemy of growth. SwiftFibre targets an annual churn rate below 8%, compared to FibreCore's published 14%. Our primary churn-reduction tool is proactive service quality monitoring: we alert customers before they notice a problem and resolve faults within 4 hours of detection wherever possible.",
                "SwiftFibre Customer Experience Strategy 2025", "churn_reduction"),

            new("Our 24-month price lock is our strongest retention instrument. Customers who know their bill will not change are customers who do not shop around. We complement this with a loyalty rewards programme that activates at month 12, offering speed upgrades and referral bonuses to long-term subscribers.",
                "SwiftFibre Customer Experience Strategy 2025", "churn_reduction"),

            new("SwiftFibre is committed to bringing genuine gigabit fibre to rural and semi-rural communities overlooked by larger providers. Our rural coverage commitment: any node we build in a rural area launches at a minimum of 100Mbps symmetric, with a roadmap targeting full 1Gbps symmetrical availability across all rural sites by end of 2026.",
                "SwiftFibre Rural Coverage Commitment — Public Statement", "rural_coverage"),

            new("We know rural customers are sceptical — they have been promised fast broadband before and let down. That is why our rural rollout includes a 90-day performance guarantee: if a rural customer does not receive at least 80% of their advertised speed during the first 90 days, they can exit with no penalty and keep the router.",
                "SwiftFibre Rural Coverage Commitment — Public Statement", "rural_coverage"),

            new("SwiftFibre's AcquisitionBlitz activates in a new coverage zone the moment the first premises go live. We run digital ads, door-to-door leaflet drops, and community social media outreach simultaneously within 2km of active coverage boundaries. Early movers get a launch discount that creates urgency and seeds word-of-mouth.",
                "SwiftFibre AcquisitionBlitz Campaign Brief", "customer_acquisition"),

            new("Our community ambassador programme is one of our most cost-efficient acquisition channels. We recruit local residents — often small business owners, community group leaders, or active social media users — to advocate for SwiftFibre in their networks. Authentic peer endorsement consistently outperforms paid advertising in new zones.",
                "SwiftFibre AcquisitionBlitz Campaign Brief", "customer_acquisition"),

            new("SwiftFibre's primary target customer is the value-conscious family household in suburban or peri-urban areas, currently paying for a FibreCore plan they feel is overpriced and underperforming. Secondary targets are home-based workers and small businesses who need reliable symmetric speeds and cannot afford the incumbent's premium business tier.",
                "SwiftFibre Target Demographics Research — Q1 2025", "target_demographics"),
        ];
    }

    // ─── Shared record types ──────────────────────────────────────────────────

    internal record ProjectSeed(Guid Id, Guid TenantId, string Name, string Description);
    internal record RequirementSeed(Guid Id, Guid ProjectId, string Content, string Status);
    internal record ContextChunkSeed(string Text, string Source, string Category);
}
