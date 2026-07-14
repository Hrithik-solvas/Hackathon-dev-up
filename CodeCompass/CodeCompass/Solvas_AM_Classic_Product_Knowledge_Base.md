# Solvas Asset Management Classic — Complete Product Knowledge Base

## 1. Executive Summary

**Solvas Asset Management Classic** (AM Classic) is an enterprise-grade, web-based portfolio management and compliance platform purpose-built for the **Collateralized Loan Obligation (CLO)** and **Collateralized Debt Obligation (CDO)** industry. It serves as the single system of record for managing the entire lifecycle of structured credit vehicles — from deal inception and asset acquisition through daily portfolio operations, regulatory compliance testing, cash flow waterfall processing, trade management, and investor reporting.

### The Problem It Solves

A CLO/CDO is a complex financial vehicle that pools together hundreds of corporate loans or bonds, then issues tranched notes backed by that collateral pool. Managing such a vehicle requires:

- Tracking hundreds of individual assets with constantly changing attributes (rates, ratings, balances, maturities)
- Ensuring the portfolio always complies with strict legal constraints defined in the deal's indenture document
- Processing thousands of financial transactions (interest payments, principal payments, trades, fee payments)
- Running sophisticated calculations (weighted averages, concentration tests, coverage ratios) on demand
- Distributing available cash through a complex priority-of-payments waterfall
- Reporting accurate portfolio information to investors, rating agencies, and regulators
- Managing multi-currency positions across global loan markets

AM Classic handles all of this in a single integrated platform with over 20 years of domain expertise built into its logic.

### Target Market

The product targets institutional players in the structured credit industry:

- **CLO Managers** (Collateral Managers) — firms like Ares, Angelo Gordon, Bardinhill who actively manage CLO portfolios
- **Corporate Trustees** — institutions like US Bank, Wilmington Trust, GLAS, Computershare who oversee deals on behalf of noteholders
- **Fund Administrators** — firms like Alter Domus, Apex, Vistra, Cortland who provide back-office services to CLO managers
- **Custodian Banks** — State Street, BNY Mellon who hold the physical assets and process settlements

### Market Position

AM Classic is a market-leading solution in the CLO administration space. It is deployed across 25+ major financial institutions globally, processing tens of billions of dollars in assets under management. The product has been in continuous development and production use for over two decades, making it one of the most battle-tested platforms in the structured credit space.

---

## 2. User Roles and Personas (Detailed)

### 2.1 Collateral Manager / Portfolio Manager

**Who they are:** Senior finance professionals responsible for maximizing returns on the CLO portfolio while maintaining compliance with the deal's legal constraints.

**What they do in AM Classic:**
- Review portfolio composition and health metrics daily
- Execute buy/sell decisions using Trading Scenarios to simulate compliance impact before committing
- Monitor Overcollateralization (OC) and Interest Coverage (IC) test cushions
- Track rating migrations and credit events across the portfolio
- Approve transactions that change portfolio composition
- Review Weighted Average metrics (WARF, WAS, WAL, Diversity Score) to make investment decisions
- Analyze concentration exposures by industry, country, obligor, and rating
- Review projected cash flows to plan reinvestment activity

**Key screens they use:** Portfolio Snapshots, Trading Scenarios, Test Results, Asset Search, Deal Summary Reports

### 2.2 Trustee Analyst

**Who they are:** Independent oversight professionals who verify compliance on behalf of noteholders.

**What they do in AM Classic:**
- Generate Portfolio Snapshots on payment dates (typically monthly or quarterly)
- Run the full compliance test suite and verify pass/fail status
- Produce official Trustee Reports that go to investors
- Compare current snapshots against prior periods to identify changes
- Verify waterfall payment calculations before cash distribution
- Investigate any test failures and determine required actions (cure amounts, trading restrictions)
- Validate data accuracy by reconciling AM Classic positions against agent reports and custodian statements

**Key screens they use:** Portfolio Snapshots, Compliance Tests, Model Management, Reports, Snapshot Compare

### 2.3 Portfolio Operations Analyst

**Who they are:** Day-to-day operations staff who maintain portfolio data integrity.

**What they do in AM Classic:**
- Process daily agent notices (rate resets, drawdowns, paydowns, fee notifications)
- Enter and verify financial transactions (interest payments, principal payments, purchases, sales)
- Maintain issuer and facility data (new issuers, facility amendments, rating updates)
- Process trade settlements and allocations
- Run data imports from external sources (Bloomberg, rating agencies, agent feeds)
- Reconcile positions against external statements
- Manage commitment amounts and unfunded commitments on revolving facilities
- Process LOC (Letter of Credit) transactions
- Handle facility merges and security exchanges when loans are refinanced

**Key screens they use:** Transaction Entry, Agent Notices, Asset Maintenance, Import Utilities, Facility/Issue Detail screens

### 2.4 Compliance / Structuring Analyst

**Who they are:** Specialists who configure and maintain the compliance rules engine.

**What they do in AM Classic:**
- Define Calculation Sequences — the ordered series of computation steps used to derive portfolio metrics
- Configure Compliance Tests with their parameters and thresholds (e.g., "Senior OC must be ≥ 120%")
- Set up Eligibility Criteria that determine what assets can be purchased
- Build and maintain Priority of Payments (waterfall) definitions
- Configure Rating Derivation rules that determine how multiple agency ratings are combined
- Set up Par Build rules that adjust principal balances for compliance purposes
- Create Dynamic Data rules that auto-populate fields based on conditions
- Manage Field Overrides when calculated values need manual correction
- Set up Import Layouts for receiving data from external systems

**Key screens they use:** Compliance Setup (Calculations, Tests, Payments, Sequence, Models, Fields, Dynamic Data Rules)

### 2.5 Tax / Accounting Professional

**Who they are:** Accountants responsible for the deal entity's financial reporting.

**What they do in AM Classic:**
- Configure Fiscal Years and reporting periods
- Run FAS 91 effective interest method amortization calculations
- Track ABS factor schedules and payment delays
- Generate Financial Statements (Income Statement, Balance Sheet)
- Manage tax lot tracking for purchase/sale positions
- Configure broker/custodian reporting mappings
- Produce year-end tax reporting packages
- Review amortization schedules and adjust cash flow expectations

**Key screens they use:** Tax Module (Fiscal Year Setup, Financial Statements, ABS Management, FAS 91 Tools)

### 2.6 System Administrator

**Who they are:** IT or operations staff responsible for system configuration.

**What they do in AM Classic:**
- Create new deals and configure their settings
- Manage user accounts, groups, and permissions
- Configure menu access and navigation layouts
- Set up enterprise integrations (ECI subscriptions, data feeds)
- Manage holiday calendars for different jurisdictions
- Configure system-level lookup codes and reference data
- Monitor system processes and job queues
- Manage cross-server exports/imports for multi-instance deployments
- Review audit logs and user activity

**Key screens they use:** System Setup (Access & Security, Menu Manager, System Settings, System Tables, Report Manager)

---

## 3. Core Business Domain: Portfolio Management (Deep Dive)

### 3.1 Deal Structure

A "Deal" in AM Classic represents a single CLO/CDO vehicle. Every piece of data in the system belongs to a deal. The deal is the top-level organizing concept.

**What a deal contains:**
- **General Information** — Deal name, closing date, reinvestment end date, legal maturity, base currency, deal type (CLO, CDO, CLO 2.0, etc.)
- **Entities (Investors/Participants)** — The tranches of notes issued by the deal (e.g., Class A Notes, Class B Notes, Subordinated Notes). Each entity has a position and receives payments according to the waterfall.
- **Sub-Entities** — Sub-accounts or sub-positions within an entity, used for tracking multiple holders of the same tranche.
- **Accounts** — Cash accounts (principal collection account, interest collection account, reserve accounts, payment accounts) that hold and disburse funds.
- **Industries** — The industry classification scheme configured for this deal (Moody's 33 industries, S&P sectors, or custom schemes).
- **Agents & Contacts** — The administrative agent, trustee, collateral manager, and other service providers involved in the deal.
- **Custom Fields** — User-defined fields that extend the data model for deal-specific tracking needs.
- **Report Groups** — Configured groupings used for reporting output.
- **Comparisons** — Saved comparison configurations for snapshot analysis.

### 3.2 Issuer / Borrower Management

An Issuer (also called Obligor or Borrower) is a company that has borrowed money. The system tracks deep information about each issuer:

**Issuer Profile:**
- Company name, identifiers (CUSIP, ticker, LEI), industry classification
- Country of domicile and revenue country allocation (a single issuer may have revenue split across multiple countries)
- Parent-subsidiary relationships (parent issuer hierarchies)
- SIC codes and GICS classification
- Analyst assignment history
- Custom fields for deal-specific categorization

**Issuer Ratings:**
- Ratings from all major agencies: Moody's (Corporate Family Rating), S&P (Issuer Credit Rating), Fitch
- Full rating history with effective dates
- Rating Outlook (Positive, Stable, Negative) with history
- Rating Watchlist status (Watch Positive, Watch Negative, Watch Developing) with history
- The system stores EVERY historical rating change — this is critical because compliance tests look at rating as of a specific point in time

**Issuer Notices:**
- Document management for notices received about the issuer
- Comments and annotations
- Transaction history associated with notices

### 3.3 Facility Management (Loans)

A Facility is a specific lending arrangement — typically a term loan or revolving credit line. This is where the bulk of daily operations happens.

**Facility Core Data:**
- Facility type (Term Loan A, Term Loan B, Revolver, Delayed Draw, etc.)
- Credit Agreement reference (the legal document governing the loan)
- Borrower information
- Original amount, currency, maturity date
- Status (Active, Defaulted, Repaid, Written Off)
- CUSIP/LoanX identifiers
- Agent bank and reporting officer

**Interest & Spread Tracking:**
- Current spread over index (e.g., LIBOR + 350bps, SOFR + 400bps)
- Full spread history with effective dates
- Index type (SOFR, LIBOR, Prime, Fixed)
- Interest rate floors and caps
- PIK (Payment-in-Kind) status — when interest is added to principal instead of paid in cash
- Spread adjustment schedules

**Commitment & Balance Management:**
- Total facility commitment amount (the maximum that can be borrowed)
- Funded amount (how much has actually been drawn)
- Unfunded commitment (difference — relevant for revolvers)
- Commitment reduction schedule (planned reductions over time)
- Full commitment history with every change logged
- Letter of Credit (LOC) sub-facilities with their own balances and transactions

**Payment Schedules:**
- Interest payment schedule (monthly, quarterly, semi-annual)
- Principal payment schedule (amortizing or bullet)
- Fee payment schedules (commitment fees, facility fees, utilization fees)
- Fee rate histories

**Facility Fees:**
- Multiple fee types per facility (commitment fee, utilization fee, facility fee, letter of credit fee)
- Fee rate history (rates can change over time)
- Fee payment details and accrual tracking
- Fee payment calculations based on unfunded/funded amounts

**Facility Transactions:**
- Every financial event is recorded as a transaction: drawdowns, paydowns, interest payments, fee payments, principal payments, rate resets
- Transactions have global impact (affect overall facility) and entity-specific impact (affect specific investor positions)
- Full transaction log with audit trail
- Transaction types are configurable per deal

**Facility Trading:**
- Par and distressed trade tracking
- Trade lifecycle: pending → settled → allocated
- Trade fees (assignment fees, delay compensation)
- Exchange rate capture for cross-currency trades
- Lot-level allocation for trades
- Trade blotter reporting

**Analytical Data:**
- Facility-level ratings (separate from issuer ratings)
- Market values with pricing source and history
- Recovery rate estimates
- Country allocation percentages
- Custom fields and custom history
- Indenture-specific categorization

### 3.4 Issue / Bond / Security Management

An Issue represents a fixed-income security (bond, note, or structured product tranche) held in the portfolio.

**Issue Core Data:**
- Issue name, CUSIP/ISIN identifiers
- Issuer reference
- Issue type (Senior Secured, Senior Unsecured, Subordinated, Mezzanine, ABS Tranche, etc.)
- Original face amount, currency
- Issue date, maturity date
- Coupon type (Fixed, Floating, PIK, Zero-coupon)
- Current coupon rate and index reference
- Factor (for amortizing securities — the current outstanding factor)
- Status (Active, Called, Matured, Defaulted)

**Interest Rate Management:**
- Full interest rate history with rate change dates
- Rate index and spread breakdown
- Interest payment period history (tracking actual accrual periods)
- Coupon change schedules
- Spread history

**Payment & Cash Flow:**
- Interest payment schedules and history
- Principal payment schedules
- Factor schedule (for ABS/MBS — tracks principal paydown over time)
- Actual vs. projected payment tracking
- Accrued interest calculations

**Tranche Structure:**
- For structured products, track the full tranche subordination stack
- Tranche seniority levels
- Subordination percentages

**Issue Ratings:**
- Issue-level ratings from Moody's, S&P, Fitch (distinct from issuer-level ratings)
- Full rating history
- Watchlist and outlook tracking
- Derived/composite ratings based on configurable derivation rules

**Servicer Tracking:**
- For ABS/MBS: track the master servicer, special servicer, backup servicer
- Servicer ratings with agency and history
- Servicer allocation percentages

### 3.5 Items (Positions / Purchase Lots)

An "Item" represents a specific position held by an entity (investor) in a facility or issue. This is the most granular level of position tracking.

**Item Data:**
- Owning entity reference
- Facility or Issue reference
- Purchase date, settlement date
- Original par amount and current par balance
- Purchase price (for discount/premium tracking)
- Current principal balance
- Exchange rate (for cross-currency positions)
- Discount/premium obligation tracking
- Custom fields

**Item Transactions:**
- Every movement that affects this specific position: allocations from global transactions, trade settlements, write-downs
- Transaction log with full audit trail

**Purchase Lots:**
- When an entity buys the same facility/issue multiple times, each purchase is tracked as a separate lot
- Lot-level cost basis for gain/loss calculation on sale
- FIFO/LIFO or specific identification for lot selection on sales

### 3.6 Credit Default Swaps (CDS)

Full lifecycle management for synthetic credit exposure:

- **CDS Contract Tracking** — Notional amount, premium, reference entity, maturity, protection buyer/seller
- **Reference Portfolios** — Baskets of reference entities for portfolio CDS
- **Credit Events** — Track credit events (failure to pay, bankruptcy, restructuring) and settlement
- **Spread History** — Track CDS spread movements over time for mark-to-market
- **Payment Schedules** — Regular premium payment tracking
- **Jurisdiction Tracking** — Legal jurisdiction for contract enforcement
- **Transactions** — All CDS-related financial flows

### 3.7 Hedges (Derivatives)

Interest rate swap and derivative management:

- **Hedge Setup** — Notional, counterparty, effective date, maturity, hedge type (swap, cap, floor, collar)
- **Payment Legs** — Each hedge has two legs (pay and receive), each with its own rate, schedule, and jurisdiction
- **Rate History** — Track interest rate changes on each leg
- **Notional Amount History** — For amortizing notionals that decrease over time
- **Payment Schedules** — Expected payment dates for each leg
- **Strike Rate Schedules** — For caps/floors/collars with scheduled strike rates
- **Projected Payments** — Forward-looking payment projections for cash flow planning

### 3.8 Equity Positions

For tracking equity or equity-like positions in the portfolio:

- **Equity Setup** — Equity name, type, market, NAV factor
- **Equity Lots** — Individual purchase lots with cost basis
- **Market Value Tracking** — Regular NAV/market value updates with history
- **Country Allocation** — Geographic revenue allocation for concentration testing
- **Equity Trades** — Trade lifecycle management for equity positions
- **Transactions** — Distributions, redemptions, capital calls

### 3.9 Cash Management

Comprehensive cash tracking across the deal's accounts:

- **Account Structure** — Multiple cash accounts per deal (Principal Collection, Interest Collection, Reserve, Payment, Reinvestment)
- **Account Purposes** — Each account is tagged with its purpose in the waterfall
- **Cash Activity Processing** — Import bank statements, match transactions, reconcile balances
- **Cash Transactions** — Every cash movement is recorded with source, destination, and type
- **Sweep Investments** — Track short-term investments of idle cash (money market, overnight investments)
- **Initial Balances** — Establish opening balances for reconciliation
- **Cash Reconciliation** — Match system-calculated balances against external bank statements

### 3.10 Agent Notices

Automated processing of communications from loan agents:

- **Notice Ingestion** — Receive and parse notices from agents (rate reset notices, payment notices, amendment notices)
- **Notice Types** — Rate change notices, drawdown notices, paydown notices, fee notices, amendment notices
- **Queue-Based Processing** — Notices enter a processing queue for review and application
- **Borrower/Lender Notices** — Separate workflows for borrower-side and lender-side notices
- **File Attachments** — Store original notice documents
- **Transaction Generation** — Convert processed notices into financial transactions
- **Batch Processing** — Handle bulk notice processing for efficiency
- **Comment Tracking** — Annotate notices with analyst comments

### 3.11 Exchange Rates & Multi-Currency Support

CLO portfolios often hold assets denominated in multiple currencies (USD, EUR, GBP, etc.):

- **Exchange Rate Management** — Store rates for all relevant currency pairs
- **Rate Loading** — Bulk import exchange rates from data providers
- **Position Conversion** — Automatically convert non-base-currency positions to deal base currency
- **FX Trades** — Track forward FX contracts used to hedge currency exposure
- **Rate History** — Full historical rates for point-in-time valuations
- **Initial Exchange Rates** — Capture rates at deal inception for gain/loss tracking

### 3.12 Market Value Tracking

Asset pricing and valuation:

- **Multiple Pricing Sources** — Support for marks from different dealers, pricing services (Markit, Bloomberg)
- **Pricing Type Classification** — Bid, Ask, Mid, Last
- **Market Value History** — Full historical pricing data per asset
- **Market Value Maps** — Configurable mappings between pricing sources and assets
- **Automated Loading** — Import market values from external feeds

---

## 4. Core Business Domain: Compliance & Structured Finance Analytics (Deep Dive)

This is the intellectual heart of the product — the compliance engine that ensures CLO/CDO vehicles operate within their legal boundaries.

### 4.1 Portfolio Snapshots

A Portfolio Snapshot is a point-in-time photograph of the entire portfolio's state. It captures every asset's attributes, balances, ratings, and classifications as of a specific date.

**Why snapshots matter:** All compliance testing is performed against snapshots, not live data. This ensures:
- Reproducible results — the same snapshot always produces the same test results
- Auditability — you can always go back and see exactly what the portfolio looked like on a specific date
- Version control — multiple snapshots can exist for the same date (e.g., "as-reported" vs. "corrected")

**Snapshot Generation Process:**
1. User selects a snapshot date and source entity
2. System pulls all current asset data as of that date from the portfolio module
3. Principal balances are calculated based on transaction history up to the snapshot date
4. Exchange rates are applied for non-base-currency assets
5. Ratings are pulled as of the snapshot date from rating history
6. Custom fields and derived values are populated
7. Snapshot is stored as an immutable record

**Snapshot Contents (per asset line):**
- Issuer information (name, country, industry)
- Asset information (facility/issue, type, maturity, coupon)
- Balance information (par amount, principal balance, market value)
- Rate information (spread, index, all-in rate, floor)
- Rating information (Moody's, S&P, Fitch — both issuer and asset level)
- Derived fields (derived rating, recovery rate, WAL contribution)
- Custom fields (deal-specific additional data)
- Classification fields (asset type category, eligibility flags)

**Snapshot Layouts:**
- Configurable column layouts determine which fields are visible and in what order
- Import layouts define the format for importing external snapshot data
- Preview layouts for on-screen viewing
- Export layouts for reporting

**Model Management:**
- A "Model" is a named configuration that combines a snapshot with its calculation setup, test definitions, and waterfall
- Models track state history (Created → Calculated → Tests Run → Approved → Locked)
- Models can be copied, compared, exported, and locked (preventing further changes)
- Multiple models can exist for the same payment date (e.g., "Preliminary" and "Final")

### 4.2 Compliance Tests (Deep Detail)

Compliance tests verify that the portfolio meets the contractual requirements defined in the deal's indenture. There are several major categories:

**4.2.1 Overcollateralization (OC) Tests**

The most critical compliance test. Verifies that the collateral pool has sufficient value to back the outstanding notes.

- Formula: OC Ratio = Adjusted Collateral Balance / Outstanding Note Balance
- Each tranche has its own OC test with a specific threshold (e.g., Class A OC must be ≥ 120.0%)
- The "Adjusted Collateral Balance" is the aggregate principal balance after applying Par Build rules (haircuts for defaulted, CCC-rated, or discounted assets)
- OC tests are tested on every payment date. A failure triggers cash diversion or trading restrictions.
- The system calculates OC adjustments including: defaulted asset haircuts, excess CCC haircuts, discount obligation adjustments, long-dated asset adjustments

**4.2.2 Interest Coverage (IC) Tests**

Verifies the portfolio generates enough interest income to cover interest payments to noteholders.

- Formula: IC Ratio = Interest Income / Note Interest Payments
- Tested per tranche (Class A IC, Class B IC, etc.)
- Interest income includes: actual interest collected, projected interest, spread adjustments
- IC adjustments include: hedge receipts, cap/floor payments, admin expense deductions
- Failure triggers typically redirect cash from equity to pay down senior notes

**4.2.3 Concentration Tests**

Verify portfolio diversification by limiting exposure to any single factor:

- **Obligor Concentration** — Maximum exposure to any single borrower (e.g., no single obligor > 2% of portfolio)
- **Industry Concentration** — Maximum exposure to any single industry (e.g., no industry > 10%)
- **Country Concentration** — Maximum exposure to any single country
- **Rating Bucket Concentration** — Maximum exposure to assets in specific rating categories (e.g., CCC or below limited to 7.5%)
- **Generic Stratification** — Configurable tests that can stratify the portfolio by any field and apply maximum/minimum limits
- **Total Concentration Limits** — Combined limits across multiple concentration dimensions

**4.2.4 Weighted Average Tests**

Verify portfolio-level metrics meet minimum or maximum thresholds:

- **WARF (Weighted Average Rating Factor)** — Must be ≤ a specified maximum (e.g., ≤ 2720)
- **WAS (Weighted Average Spread)** — Must be ≥ a specified minimum (e.g., ≥ 3.50%)
- **WAL (Weighted Average Life)** — Must be ≤ a specified maximum (e.g., ≤ 5.0 years)
- **Weighted Average Coupon** — Minimum coupon rate
- **Diversity Score** — Moody's proprietary metric measuring issuer diversification (must be ≥ a minimum)

**4.2.5 Eligibility Criteria**

Rules that determine whether a specific asset is eligible to be included in the portfolio. These are tested per-asset (not portfolio-wide):

- Rating requirements (minimum rating to qualify)
- Maturity constraints (asset cannot mature after the deal's reinvestment period)
- Currency restrictions
- Loan type restrictions (only senior secured, only first-lien, etc.)
- Minimum par amount requirements
- Defaulted asset exclusions
- PIK asset restrictions

**4.2.6 Custom Tests**

The system supports custom-coded compliance tests using configurable SQL-based logic for deal-specific requirements that don't fit standard test types.

### 4.3 Calculation Sequences & Blocks (The Computation Engine)

This is the configurable calculation framework that powers all compliance analytics.

**Calculation Sequence:**
A Calculation Sequence is an ordered list of steps that the system executes to compute portfolio metrics. Each deal can have its own customized sequence.

**Calculation Blocks (Building Blocks):**
Individual computation units that perform specific calculations. Each block takes parameters and produces results. Available block types include:

- **Balance Calculations** — Compute principal balances, adjusted balances, par build adjustments
- **Weighted Average Spread** — Calculate portfolio WAS using configurable spread definitions
- **Weighted Average Life** — Calculate portfolio WAL using payment projections
- **Weighted Average Rating** — Calculate WARF using configurable rating factor tables
- **Weighted Average Coupon** — Calculate portfolio average coupon
- **Diversity Score** — Calculate Moody's diversity score or ABS diversity score
- **Herfindahl Score** — Alternative diversity metric
- **Fitch Sector Score** — Fitch's proprietary sector diversity metric
- **Stratification** — Group assets by a field and calculate sub-totals (e.g., by rating, by industry)
- **Concentration Checks** — Test exposure limits
- **Portfolio PB Adjustment** — Apply par build rules to adjust collateral values
- **Excess Concentration Haircut** — Calculate haircuts for over-concentrated positions
- **Field Calculator** — Perform arithmetic on snapshot fields
- **Field Overrides** — Apply manual override values
- **Formula** — Execute configurable mathematical formulas
- **Interest Calculations** — Compute accrued interest, projected interest
- **Cash Calculations** — Compute available cash amounts
- **Payment Amount** — Calculate waterfall payment step amounts
- **Liability Balance** — Track note/liability balances
- **Liability Rating** — Determine note rating for IC calculations
- **Projected Payments** — Generate forward-looking payment projections
- **Reinvestment Rates** — Apply reinvestment assumptions
- **Custom SP (Stored Procedure)** — Execute deal-specific custom calculations

**Calculation Groups:**
Calculations can be grouped into logical sets (e.g., "Balance Calculations", "Rating Calculations", "Waterfall Calculations") for organizational clarity.

**Alternative Calculation Sequences:**
Some deals require different calculation logic for different scenarios (e.g., reinvestment period vs. amortization period). Alternative sequences allow switching logic based on deal state.

### 4.4 Rating Derivation

A critical compliance feature — determining the "derived" or "composite" rating for each asset from multiple agency inputs.

**The Problem:** Rating agencies often rate the same issuer/issue differently. The indenture specifies rules for which rating to use (e.g., "use the lower of Moody's and S&P" or "if only one agency rates it, notch it down by one").

**How AM Classic handles it:**
- **Rating Derivation Scenarios** — Named configurations of derivation rules
- **Rating Derivation Rules** — Prioritized rules that define: "If Moody's = X and S&P = Y, then derived rating = Z"
- **Security-Level vs. Issuer-Level** — Derivation can work at the issue level or fall back to issuer level
- **Recovery Rate Derivation** — Similar rule-based system for deriving recovery rate estimates
- **Processing** — Derivation runs during snapshot generation, applying rules to produce a single derived rating per asset

### 4.5 Par Build Rules

Par Build rules adjust the principal balance of assets for compliance test purposes. This is distinct from the actual accounting balance.

**Why needed:** Indentures specify that certain types of assets should count for less than par in compliance calculations:
- Defaulted assets might be valued at market price or recovery rate instead of par
- CCC-rated assets in excess of a threshold might be valued at market or a haircut
- Long-dated assets might be adjusted
- Discount obligations might be limited to purchase price rather than par

**Implementation:**
- **Rule Groups** — Named collections of par build rules
- **Individual Rules** — Each rule specifies: condition (what assets it applies to) + method (how to adjust the balance)
- **Methods** — Par, Market Value, Recovery Rate, Purchase Price, Lower of Market/Recovery, Custom Formula
- **Priority** — Rules are processed in order; first matching rule wins
- **Result** — Each asset gets a "PB Method" and an "Adjusted Principal Balance" for use in OC calculations

### 4.6 Priority of Payments (Waterfall)

The waterfall defines how available funds are distributed on each payment date. This is the most complex configuration in the system.

**Waterfall Structure:**
- A "Priority of Payments" (POP) is the top-level container
- It contains multiple "Payment Sequences" (e.g., Interest Waterfall, Principal Waterfall)
- Each sequence contains ordered "Payment Steps" — each step defines a specific disbursement

**Payment Step Types:**
- **Admin Expense Payment** — Pay deal expenses (trustee fees, legal fees, audit fees)
- **CM Fee Payment** — Pay the collateral manager's management fee
- **Note Interest Payment** — Pay periodic interest to a specific note class
- **Note Principal Payment** — Pay principal to a specific note class
- **OC Test Cure** — Divert cash to cure an OC test failure (pay down senior notes)
- **IC Test Cure** — Divert cash to cure an IC test failure
- **Account Transfer** — Move cash between accounts
- **Amount Transfer** — Transfer a calculated amount
- **Equity Note Distribution** — Distribute residual cash to equity holders
- **Expense Fee Payment** — Pay specific fees
- **Residual Fee Payment** — Pay incentive/performance fees
- **Group Payment** — Pay a group of payees proportionally

**Waterfall Execution:**
1. Start with available cash in collection accounts
2. Process each payment step in priority order
3. Each step calculates its required amount and pays from available cash
4. If cash is insufficient, lower-priority steps receive less or nothing
5. OC/IC cure mechanisms redirect cash from equity to senior notes when tests fail
6. Results are captured showing what each step paid and what remains

### 4.7 Trading Scenarios (What-If Analysis)

Allows users to simulate trades and immediately see their compliance impact before execution.

**How it works:**
1. Start from an existing Portfolio Snapshot
2. Add hypothetical trades — purchases (adds assets) and sales (removes assets)
3. System regenerates the snapshot with the trades applied
4. Run full compliance test suite against the modified snapshot
5. Compare results: Current snapshot vs. Post-trade scenario
6. User decides whether to proceed with the real trade

**Key features:**
- Multiple trades per scenario
- Eligibility check for new purchases
- Cash sufficiency check — verify enough cash exists to fund the purchases
- Side-by-side comparison of all test results (before vs. after)
- Scenario import — bulk load trade scenarios from files
- Scenario approval workflow (maker-checker)
- Conversion — approved scenarios can be converted into actual trades

### 4.8 Effective Spread Calculations

Advanced spread analytics that account for LIBOR/SOFR floors, prepayment expectations, and other factors:

- Effective spread vs. stated spread (when floors are in-the-money, effective spread exceeds stated spread)
- Configurable effective spread settings per deal
- Floor benefit calculations
- Used in WAS calculations for more accurate compliance testing

### 4.9 Field Overrides

Manual correction mechanism for when calculated or imported values need adjustment:

- Override any snapshot field for any asset
- Overrides persist across snapshot regenerations
- Effective date ranges — overrides can be temporary
- Override import — bulk load overrides from files
- Override history and audit trail
- Overrides are marked visibly in snapshot displays so users know values have been manually adjusted

---

## 5. Core Business Domain: System Administration & Configuration (Deep Dive)

### 5.1 Security & Access Control

Multi-layered security model:

- **Authentication** — Windows Integrated (NTLM/Kerberos), Forms-based login, or SSO (Single Sign-On via OpenID)
- **User Groups** — Define permission sets (e.g., "Read-Only Analyst", "Full Operations", "Admin")
- **Menu Access** — Control which navigation items each group can see (fine-grained at individual menu item level)
- **Object Access** — Control CRUD permissions on specific data objects
- **Entity Access** — Restrict users to see only specific entities (investors) within a deal
- **Asset Group Access** — Restrict users to see only specific asset groups
- **Deal-Level Access** — Users may only have access to specific deals
- **Login History** — Track all login events with timestamps and session IDs
- **Maker-Checker** — Dual-control for sensitive operations (one user initiates, another approves)

### 5.2 Configuration System

Highly configurable at multiple levels:

- **System-Level Configuration** — Global settings affecting all deals
- **Deal-Level Configuration** — Per-deal settings that override system defaults
- **Configuration Categories** — Settings organized by functional area (Portfolio, Compliance, Tax, System)
- **Product Modules** — Enable/disable entire functional modules per deal
- **Lookup Codes** — Configurable reference data tables (dozens of lookup types: transaction types, status codes, fee types, asset categories, etc.)
- **Custom Fields / User Defined Fields** — Extend the data model without database changes. Available on Issuers, Facilities, Issues, Items, Entities.
- **Calendar Management** — Business day calendars per jurisdiction (determines payment dates, accrual days)
- **Holiday Schedules** — Configure non-business days for each calendar

### 5.3 Reporting

Comprehensive reporting framework:

- **SSRS Integration** — Reports defined as RDL files rendered by SQL Server Reporting Services
- **Report Groups** — Organize reports into logical categories
- **Report Parameters** — Configurable parameters for each report (date ranges, entities, formats)
- **Report Manager** — Admin interface for managing report definitions and access
- **Agent Notices Reports** — Formatted notice outputs
- **Compliance Reports** — Test result summaries, snapshot details, waterfall results
- **Portfolio Reports** — Position reports, transaction summaries, rating distribution
- **Export to Excel** — Most grids and reports export to Excel for further analysis

### 5.4 Data Import / Export Framework

Extensive ETL capabilities:

- **Import Layouts** — Configurable file format definitions (column mapping, value mapping, data types)
- **Supported Formats** — Delimited files (CSV, TSV), fixed-width, Excel
- **Value Maps** — Translate external codes to internal codes during import (e.g., external rating "Baa1" → internal code "7")
- **Validation** — Import processes validate data before committing
- **Error Handling** — Detailed error logs showing rejected rows and reasons
- **Import Types Available:**
  - Portfolio data (positions, transactions)
  - Rating data (from Moody's, S&P, Fitch feeds)
  - Market values (from pricing services)
  - Exchange rates (from FX providers)
  - Bloomberg issue data
  - Agent notice data
  - Compliance snapshot data
  - Trade data
  - Accruals data
- **Export Utilities** — Configurable data exports for feeding downstream systems
- **Transaction Export** — Export transactions in configurable formats for accounting systems

### 5.5 Enterprise Communication Interface (ECI)

Event-driven integration framework for real-time data exchange:

- **Event Types** — Business events that trigger notifications (new transaction, rating change, trade settlement, etc.)
- **Subscriptions** — External systems subscribe to specific event types for specific deals
- **Event Logging** — Full audit trail of all events published and consumed
- **Subscription Manager** — Configure which external systems receive which events
- **Object Mapping** — Map internal business objects to external system identifiers
- **File-Based Interface** — Events can be published as files for batch processing
- **Bidirectional** — Both publish (outbound) and subscribe (inbound) capabilities

### 5.6 Cross-Server Export/Import (CSES)

For multi-instance deployments where data needs to move between AM Classic installations:

- **Export Compliance** — Export compliance configuration (tests, sequences, calculations) from one instance
- **Import Compliance** — Import into another instance
- **Export Portfolio Snapshots** — Move snapshot data between instances
- **Merge Capabilities** — Merge imported data with existing data
- **Tablespace Configuration** — Map source/target database structures

---

## 6. Tax & Accounting Module (Deep Dive)

### 6.1 Fiscal Year Management

- Define fiscal year periods for each deal entity (calendar year or custom periods)
- Configure reporting periods within each fiscal year (monthly, quarterly)
- Track period open/close status
- Control which transactions fall into which period

### 6.2 FAS 91 (ASC 310-20) Effective Interest Amortization

Implements the accounting standard for amortizing loan origination fees, costs, premiums, and discounts over the life of the loan:

- **Item-Level Tracking** — Each purchase lot has its own amortization schedule
- **Cash Flow Expectations** — Define expected principal and interest cash flows
- **Effective Interest Rate Calculation** — System computes the yield that equates expected cash flows to carrying value
- **Period Amortization** — Calculate the amortization amount for each reporting period
- **Prospective Recalculation** — When cash flow expectations change, system recalculates going forward (prospective method per ASC 310-20)
- **Expected Interest Rate Schedules** — For variable-rate loans, project future index rates
- **Actual vs. Expected Tracking** — Compare actual payments received against expectations
- **Override Capabilities** — Manual override of cash flow expectations when needed
- **Event Tracking** — Track events that trigger recalculation (paydowns, amendments, defaults)

### 6.3 ABS Factor Management

For asset-backed securities where principal pays down over time:

- **Factor Schedules** — Track principal factor changes over time
- **Payment Delay Configuration** — ABS often have a delay between record date and payment date
- **Amortization Schedule Tracking** — Expected vs. actual principal paydown
- **Deal-Level ABS Configuration** — Global settings for ABS factor processing

### 6.4 Financial Statements

Generate standard financial reports for deal entities:

- **Income Statement** — Interest income, fee income, expenses, gains/losses, net income
- **Balance Sheet** — Assets (loans at amortized cost), liabilities (notes outstanding), equity
- **Supporting Schedules** — Detailed breakdowns of income/expense categories
- **Statement Comments** — Annotate financial statements with explanatory notes
- **Statement Caching** — Cache computed statements for performance
- **Multi-Entity Support** — Generate statements per entity or consolidated

### 6.5 Broker Configuration

Map deal structures to external broker/custodian reporting:

- **Broker-Deal Mapping** — Associate deals with broker accounts
- **Tax Entity Configuration** — Map internal entities to tax reporting entities
- **Account Balance Tracking** — Tax basis account balances for broker reporting

---

## 7. Integration & Data Feed Ecosystem (Deep Dive)

### 7.1 Bloomberg Integration

- **Issue Data Import** — Pull security master data from Bloomberg (coupon, maturity, ratings, identifiers)
- **Index Rate Loading** — Daily import of index rates (SOFR, LIBOR legacy, Prime, EURIBOR, etc.) with full history
- **Market Value Import** — Pull pricing data for mark-to-market

### 7.2 Rating Agency Feeds

- **Moody's RDS (Rating Data Service)** — Automated import of Moody's corporate family ratings, outlooks, watchlists
- **S&P/Fitch** — Rating import from other agencies
- **Rating history is immutable** — once imported, historical ratings are preserved for audit

### 7.3 Loan Agent Interfaces

The system interfaces with major loan agents who administer syndicated loans:

- **US Bank (Corporate Trust & Loan Services)** — Position reconciliation, notice processing
- **GLAS** — European loan agent interfaces
- **Computershare** — Notice processing, position feeds
- **Cortland** — Agent data integration
- **Wilmington Trust** — Trustee data feeds

Data flows include: position statements, transaction confirmations, rate change notices, payment notices.

### 7.4 Custodian Interfaces

- **State Street** — Cash activity feeds, position reconciliation
- **BNY Mellon** — Cash statements, settlement data
- **Cash Activity File Processing** — Parse bank statements into individual transactions for reconciliation

### 7.5 GoldStar Navigator (GNV)

A specialized data feed interface:

- **Entity-Level Processing** — Process data at the investor/entity level
- **Facility-Level Mapping** — Map external facility references to internal IDs
- **File Processing Logs** — Track all files processed with status and errors
- **Reconciliation** — Compare GNV data against AM Classic positions

### 7.6 LoanIQ / Wall Street Office

Via the ECI framework, AM Classic can exchange data with these mainstream loan servicing platforms.

---

## 8. Typical User Workflows (Detailed End-to-End)

### 8.1 Monthly Payment Date Processing (Trustee Workflow)

This is the most important recurring workflow, typically happening monthly or quarterly:

1. **T-5 days: Data Preparation**
   - Import latest exchange rates
   - Import latest market values
   - Process any outstanding agent notices
   - Verify all transactions are entered up to the determination date
   - Update any rating changes received since last payment date

2. **T-3 days: Snapshot Generation**
   - Select the determination date (typically 5 business days before payment)
   - Generate Portfolio Snapshot — system pulls all data as of that date
   - Review snapshot for completeness — check asset count, total par, missing data flags
   - Address any data quality issues (missing ratings, missing market values)

3. **T-2 days: Calculations & Testing**
   - Run Calculation Sequence against the snapshot (computes all metrics: WAS, WARF, WAL, Diversity, etc.)
   - Run full Compliance Test suite (OC, IC, concentration, eligibility)
   - Review test results — identify any failures
   - If failures exist: determine cure actions (cash diversion amounts)
   - Run the Priority of Payments (waterfall) to determine payment amounts per tranche

4. **T-1 day: Review & Approval**
   - Compare results against prior period (use Model Compare feature)
   - Review waterfall payment amounts
   - Lock the model (prevents further changes)
   - Generate Trustee Report for distribution to investors

5. **Payment Date: Execution**
   - Cash is distributed per waterfall results
   - Payment transactions are recorded in the system
   - Account balances are updated
   - Model state moves to "Published"

### 8.2 Trade Execution Workflow (Collateral Manager)

1. **Identify Opportunity** — Manager identifies an asset to buy or sell
2. **Create Trading Scenario** — Enter the hypothetical trade (sell Asset X at 98, buy Asset Y at par)
3. **Run Compliance Check** — System shows pre-trade vs. post-trade compliance metrics
4. **Verify Eligibility** — For purchases, system checks if the new asset meets all eligibility criteria
5. **Check Cash Sufficiency** — Verify deal has enough cash to fund the purchase
6. **Approve Scenario** — If compliant, approve the trading scenario
7. **Convert to Trade** — System creates the actual trade records from the approved scenario
8. **Enter Trade Details** — Confirm settlement date, counterparty, exact price, fees
9. **Settle Trade** — On settlement date, process the actual cash/position movements
10. **Allocate** — Assign the trade across entities/lots
11. **Post-Trade Snapshot** — Generate new snapshot confirming compliance is maintained

### 8.3 New Deal Onboarding Workflow (Implementation)

1. **Deal Creation** — Use Deal Add wizard to create the deal shell with basic information
2. **Configure System Tables** — Set up lookup codes, transaction types, fee types specific to this deal
3. **Set Up Entities** — Create note classes (Class A, B, C, D, Equity) with face amounts and rates
4. **Set Up Accounts** — Create cash accounts with purposes (Principal, Interest, Reserve)
5. **Configure Industry Classification** — Select or customize the industry scheme (Moody's 33, S&P, or custom)
6. **Import Issuer Data** — Bulk load issuers with ratings and industry classifications
7. **Import Facilities/Issues** — Load the initial asset portfolio from closing documents
8. **Set Up Compliance:**
   - Define or copy Calculation Sequence from a template deal
   - Configure Compliance Tests with thresholds from the indenture
   - Set up Rating Derivation rules per the indenture's rating methodology
   - Define Par Build rules based on indenture provisions
   - Build the Priority of Payments waterfall per the indenture schedule
   - Configure Eligibility Criteria
9. **Load Historical Data** — Import transaction history if deal is not brand new
10. **Generate Initial Snapshot** — Create the first compliance snapshot and verify test results match closing expectations
11. **Configure Reports** — Set up report templates and distribution lists
12. **User Setup** — Create user accounts and assign appropriate group permissions
13. **UAT** — Client tests all workflows before go-live

### 8.4 Credit Event / Default Handling

1. **Identify Default** — Rating downgrade triggers or agent notice confirms a credit event
2. **Record Default** — Update facility/issue status to "Defaulted" with effective date
3. **Par Build Impact** — System automatically applies par build rules (defaulted asset valued at recovery rate or market value instead of par)
4. **Generate Updated Snapshot** — Create a new snapshot reflecting the default
5. **Run Compliance Tests** — Check if OC/IC tests still pass after the haircut
6. **If Test Fails** — Determine cure amount needed and execute waterfall with cure mechanism
7. **Track Recovery** — Over time, track actual recovery payments received on the defaulted asset
8. **Write-Down** — If asset is determined to be worthless, process write-down transaction

---

## 9. Business Glossary (Comprehensive)

| Term | Detailed Definition |
|------|---------------------|
| **CLO** | Collateralized Loan Obligation — a structured finance vehicle that pools together leveraged loans (bank loans to below-investment-grade companies) and issues tranched notes backed by the pool. |
| **CDO** | Collateralized Debt Obligation — broader term for vehicles that pool various types of debt instruments (bonds, loans, ABS). CLOs are a specific type of CDO. |
| **Deal / Vehicle** | The legal entity (typically a Cayman Islands SPV) that owns the loan portfolio and issues the notes. In AM Classic, this is the top-level data container. |
| **Indenture** | The master legal document governing a CLO/CDO. Defines all tests, thresholds, waterfall rules, eligibility criteria, and trading restrictions. The entire compliance configuration in AM Classic is a digital representation of the indenture. |
| **Tranche** | One class of notes within the deal's capital structure (e.g., Class A-1, Class A-2, Class B, Class C, Subordinated Notes, Equity). Senior tranches get paid first; junior tranches bear more risk. |
| **Collateral Manager** | The investment management firm hired to actively manage the loan portfolio — selecting which loans to buy/sell within the indenture's constraints. |
| **Trustee** | An independent institution (bank) that oversees the deal's compliance, verifies test results, and processes payments. The trustee protects noteholders' interests. |
| **Issuer / Obligor / Borrower** | The company that has borrowed money via the loan or bond that sits in the CLO portfolio. |
| **Facility** | A specific loan facility (term loan, revolving credit line) under a credit agreement. This is the primary asset type in most CLOs. |
| **Issue / Security** | A bond or note — a traded debt instrument with a CUSIP/ISIN identifier. |
| **Item / Position / Lot** | The CLO's specific holding of a facility or issue — the amount owned and cost basis. |
| **Entity / Investor** | A noteholder or participant in the deal. Each tranche of notes is represented as an entity. |
| **Sub-Entity** | A sub-account within an entity — used when multiple holders own the same tranche class. |
| **Credit Agreement** | The legal document between borrowers and lenders that establishes the loan facility terms. |
| **Agent (Administrative Agent)** | The bank that administers the loan — processes payments, sends notices, maintains records. (Not to be confused with the deal Trustee.) |
| **Portfolio Snapshot** | An immutable point-in-time capture of all portfolio data used as the basis for compliance testing. |
| **Model** | A named compliance workspace combining a snapshot with calculation configuration, test results, and waterfall output. |
| **OC (Overcollateralization)** | The excess of collateral value over note face value. OC ratio = Collateral / Notes. Must exceed minimum thresholds. |
| **IC (Interest Coverage)** | The excess of interest income over interest expense. IC ratio = Income / Payments. Must exceed minimum thresholds. |
| **WARF** | Weighted Average Rating Factor — converts letter ratings to numeric scores (using Moody's scale), then computes the weighted average across the portfolio. Lower is better (higher credit quality). |
| **WAS** | Weighted Average Spread — the par-weighted average spread over index across all floating-rate assets. Higher is better (more income). |
| **WAL** | Weighted Average Life — the par-weighted average remaining time to expected principal repayment. Shorter is typically safer. |
| **Diversity Score** | Moody's metric that maps actual issuers to "industry-equivalent" independent issuers. Higher score = more diversified. |
| **Par Build** | Rules that adjust the carrying value of assets for compliance calculations. Certain assets (defaulted, deeply discounted, excess CCC) are counted below par. |
| **Waterfall / Priority of Payments** | The strict payment hierarchy defining who gets paid and in what order from available deal cash. |
| **Reinvestment Period** | The time window (typically 4-5 years from closing) during which the manager can reinvest principal proceeds into new loans. After this period, principal is used to pay down notes. |
| **Determination Date** | The date on which portfolio composition is "frozen" for purposes of compliance testing (typically 5 business days before payment date). |
| **Payment Date** | The date on which the deal makes interest and principal payments to noteholders per the waterfall. |
| **Concentration Limit** | Maximum percentage of the portfolio that can be exposed to any single factor (obligor, industry, country, rating bucket). |
| **Eligibility Criteria** | Rules defining what types of assets qualify for purchase into the portfolio. |
| **PIK (Payment in Kind)** | When a borrower pays interest not in cash but by adding the interest amount to the loan balance. |
| **LOC (Letter of Credit)** | A sub-facility type where the lender provides a guarantee rather than funding cash. |
| **FX Trade** | A foreign exchange transaction to hedge currency exposure on non-base-currency assets. |
| **Settlement** | The actual exchange of cash and legal ownership when a trade completes. |
| **Accrued Interest** | Interest that has been earned but not yet paid since the last payment date. |
| **Factor** | For amortizing securities, the current outstanding principal as a fraction of original face amount. |
| **CUSIP** | Committee on Uniform Securities Identification Procedures — a 9-character identifier for North American securities. |
| **LoanX** | Markit's loan identification system — widely used for identifying syndicated loan facilities. |
| **SOFR** | Secured Overnight Financing Rate — the benchmark interest rate that replaced LIBOR for USD floating-rate loans. |
| **Spread** | The margin above a base index rate that the borrower pays (e.g., SOFR + 350bps means 3.5% above SOFR). |
| **Floor** | A minimum index rate that applies regardless of actual market rates (e.g., SOFR floor of 1% means even if SOFR is 0.5%, the effective base rate is 1%). |
| **Recovery Rate** | The percentage of par value expected to be recovered on a defaulted asset. |
| **Loss Given Default (LGD)** | 1 minus the Recovery Rate — the expected loss percentage if an asset defaults. |
| **Maker-Checker** | A dual-control process requiring separate users to initiate and approve sensitive actions. |

---

## 10. Client Deployment Model & Customization

### Deployment Architecture

Each client receives a dedicated deployment:
- Separate SQL Server database(s)
- Dedicated IIS web application instance
- Client-specific Windows services for enterprise processing
- Isolated from other clients' data and processes

### Client-Specific Customization

The system supports extensive per-client customization without forking the core codebase:

- **Custom Stored Procedures** — Client-specific calculation logic
- **Custom Reports** — Tailored RDL report definitions
- **Custom Import Layouts** — Mappings specific to each client's data sources
- **Custom Compliance Logic** — Deal-specific calculation blocks
- **Custom Fields** — Per-deal additional data attributes
- **Custom Menu Configuration** — Client-specific navigation structure
- **Custom Lookup Codes** — Client-specific reference data values

### Active Client Installations

| Client | Type | Specialty |
|--------|------|-----------|
| Alter Domus | Fund Administrator | European CLO administration |
| Angelo Gordon | Asset Manager | Credit-focused investing |
| Apex | Fund Administrator | Global fund services |
| Ares | Asset Manager | Major CLO manager |
| Bardinhill | Asset Manager | Credit strategies |
| BNY Mellon | Custodian/Trustee | Major global custodian |
| Computershare | Corporate Trust | Trustee services |
| Cortland | Fund Administrator | Structured credit admin |
| Deutsche Bank | Investment Bank | Structured products |
| Deerpath | Asset Manager | Direct lending |
| Delaware Trust | Corporate Trust | Trustee services |
| GLAS | Corporate Trust | European trustee |
| ManGLG | Asset Manager | Credit management |
| MeDirect | Bank | Investment platform |
| Siepe | Technology/Services | Data services |
| State Street | Custodian | Major global custodian |
| Tanarra | Asset Manager | Credit strategies |
| US Bank CT | Corporate Trust | Major US trustee |
| US Bank LS | Loan Services | Loan servicing |
| Vibrant | Asset Manager | Credit investing |
| Vistra | Fund Administrator | Global corporate services |
| WAB | Bank | Banking services |
| Wilmington | Corporate Trust | Trustee services |
| WSFS | Bank | Banking/trust services |

---

## 11. Technology Architecture Overview

### Web Layer
- **Classic ASP (VBScript)** — Server-side web application generating HTML
- **IIS (Internet Information Services)** — Web server hosting
- **Session Management** — ASP session with SQL Server session state fallback
- **Security** — Windows Authentication (NTLM/Kerberos) or Forms-based with encrypted credentials
- **Architecture Pattern** — Thin client (all logic server-side), thick database (business logic in stored procedures)

### Database Layer
- **SQL Server** — Primary data store (all editions from Standard to Enterprise)
- **Stored Procedure Heavy** — 1000+ stored procedures containing business logic
- **Partitioning** — Large tables partitioned for performance (transaction tables, snapshot tables)
- **Service Broker** — Asynchronous processing for long-running operations (snapshot generation, compliance test execution)
- **SQL CLR** — .NET assemblies hosted in SQL Server for complex calculations
- **Schema Design** — Three logical schemas: Core (system/security), Portfolio (assets/transactions), Compliance (snapshots/tests)

### Enterprise Services Layer
- **.NET Windows Services** — Background processing (queue processors, event publishers, data loaders)
- **.NET Console Applications** — Batch processes (scheduled data imports, report generation)
- **Core System Service** — Processes system-level queues
- **Data Reconciliation Service** — Automated position reconciliation
- **Event Communication Interface** — Publishes/subscribes to business events
- **Snapshot Layout Automation** — Automated snapshot generation on schedule
- **Solvas Loaders** — Specialized data import processors

### Reporting Layer
- **SQL Server Reporting Services (SSRS)** — Enterprise report rendering
- **RDL Files** — Report Definition Language templates
- **Parameterized Reports** — Dynamic reports with user-selected criteria
- **Scheduled Reports** — Automated report generation and distribution

---

## 12. Competitive Advantages & Value Proposition

### Why Clients Choose AM Classic

1. **Deep Domain Expertise** — 20+ years of structured credit knowledge encoded in the system. Every edge case from real CLO/CDO operations has been encountered and handled.

2. **Configurable Compliance Engine** — No hardcoded test logic. Every calculation, test, threshold, and waterfall step is configurable through the UI. When a new indenture introduces novel provisions, the system can accommodate them without code changes.

3. **End-to-End Coverage** — Single platform for portfolio management + compliance + waterfall + trading + tax. No need to stitch together multiple point solutions.

4. **Audit Trail** — Every change to every field is logged. Critical for regulated environments where trustees must demonstrate accurate, reproducible results.

5. **Trading Scenario Analysis** — Real-time what-if analysis lets managers make informed buy/sell decisions without guessing about compliance impact.

6. **Multi-Deal Scale** — A single installation can manage hundreds of deals simultaneously with proper data isolation and access control.

7. **Proven at Scale** — Processing tens of billions in assets across 25+ major institutions. The system handles the volume and complexity of the largest CLO platforms in the market.

8. **Flexible Integration** — Open architecture for connecting to any external data source or downstream system via configurable import/export and the ECI event framework.

9. **Client-Specific Customization** — Each client can have tailored workflows, reports, and business logic without requiring a code fork, thanks to the configurable architecture.

10. **Comprehensive Data Model** — The system's data model captures every nuance of structured credit instruments: floating rates, payment schedules, commitment histories, rating migrations, multi-currency positions, derivative overlays, and more.

### Key Differentiators from Competitors

- **Waterfall Engine** — The Priority of Payments implementation is exceptionally detailed, supporting complex waterfall structures with conditional steps, cure mechanisms, and circular references that simpler systems cannot handle.
- **Calculation Sequence Architecture** — The modular, reorderable calculation framework means no two deals need to use the same computation logic. Each indenture's unique requirements are accommodated.
- **Rating Derivation Flexibility** — Supports arbitrary rating agency combination rules, not just simple "lower of two" logic.
- **Snapshot Versioning** — Multiple snapshots per date with full comparison capabilities enables "what changed" analysis that auditors require.

---

## 13. Product Modules Summary

| Module | Description | Key Use Case |
|--------|-------------|--------------|
| **Portfolio** | Core asset/transaction management | Always on — fundamental data management |
| **Compliance** | Snapshot generation, compliance testing, waterfall | Trustee reporting, manager oversight |
| **Tax** | FAS 91, fiscal year, financial statements | Fund accounting, year-end reporting |
| **Trading** | Trade lifecycle, lot allocation, settlement | Active management, secondary market activity |
| **CDS** | Credit default swap management | Synthetic credit exposure tracking |
| **Hedges** | Interest rate derivative management | Rate risk mitigation tracking |
| **Agent Notices** | Automated notice processing | Operations efficiency for high-volume loans |
| **ECI** | Enterprise event-driven integration | Real-time data exchange with external systems |
| **Digitize** | Document digitization pipeline | Automated data extraction from documents |

---

## 14. Summary

Solvas AM Classic is a mission-critical enterprise platform that enables the structured credit industry to operate. Without it (or a system like it), the complexity of managing a CLO — tracking hundreds of assets with constantly changing rates, ratings, and balances; running dozens of compliance tests on every payment date; distributing cash through intricate waterfalls; and reporting to investors and regulators — would be unmanageable at scale.

The product's value lies in its combination of deep domain knowledge, extreme configurability, comprehensive data capture, and proven reliability across 20+ years and 25+ major institutional deployments. It transforms what would be thousands of spreadsheets and manual calculations into an automated, auditable, integrated workflow that runs the CLO industry's back office.
