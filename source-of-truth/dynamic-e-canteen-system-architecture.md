# Dynamic E-Canteen System - Core Architecture

## Purpose

This document defines the canonical product concept for the canteen platform. It captures the business model, domain boundaries, operating rules, and key architecture decisions for a multi-tenant canteen management system that supports educational institutes, corporate clients, and restaurant/QSR operations.

## 1. Architectural Principles

1. **Single product, many tenants**
   - One codebase.
   - Tenant-specific behavior driven by configuration.
   - No hard-coded client assumptions.

2. **Reserve first, settle on delivery**
   - Wallet amount is blocked when the order is placed.
   - Inventory is reserved when the order is placed.
   - Final deduction happens when the order is delivered/collected.

3. **Emergency balance is a system safety net**
   - Not directly rechargeable.
   - Used only when the main balance is insufficient.
   - Recovered automatically on the next recharge.

4. **Offline-first for operational continuity**
   - Canteen counters must keep working during network failures.
   - Local operations should continue and sync later.

5. **Role-based access and auditability**
   - Every action must be attributable to a role and user.
   - Sensitive flows require audit logs.

6. **Configuration over customization by code**
   - Client type, payment model, operating hours, pricing rules, and channels must be configurable.

---

## 2. Target Client Types

### 2.1 Educational Institute
- Students, teachers, staff, parents.
- NFC-first identity and ordering.
- Prepaid wallet as the primary model.
- Emergency balance enabled.
- Optional parent controls and spending limits.

### 2.2 Corporate
- Employees, department heads, HR admins.
- Subsidy and allowance support.
- Mostly wallet-based with optional postpaid or hybrid settlement.
- Strong reporting and department-wise analytics.

### 2.3 Restaurant / QSR
- Anonymous customers and registered customers.
- QR-first ordering.
- Table/queue management.
- Online payment or cash at counter.
- Loyalty support for registered users.

---

## 3. Multi-Tenant Model

### 3.1 Client Configuration
Each tenant should control:
- Client type: `Educational`, `Corporate`, `Restaurant/QSR`
- Operational mode: `Prepaid`, `Postpaid`, `Hybrid`, `Cash`
- Authentication methods: `NFC`, `QR`, `Mobile App`, `Biometric`
- Menu type: `Pre-cooked`, `Made-to-order`, `Hybrid`
- Payment gateway enablement
- Branding: logo, colors, terminology
- Operating hours: breakfast, lunch, dinner, snacks
- Billing cycle for postpaid customers
- Notification preferences
- Ordering rules and cancellation windows
- Tax settings and receipt formatting

### 3.2 Tenant Isolation
- Data must be logically isolated by `ClientId`.
- Reporting and operational views must respect tenant boundaries.
- Super Admin may view all tenants; Client Admin only their own.

---

## 4. Role-Based Access Control

### 4.1 Global Roles
- **Super Admin**: manages all tenants, configuration, and cross-client analytics.
- **Client Admin**: manages tenant settings, users, menu, reports, operators, and finance.
- **Operator / Canteen Staff**: manages orders, stock, kitchen display, delivery completion.

### 4.2 Educational Roles
- **Teacher / Staff**: order food, recharge, view own history, optionally use credit.
- **Student**: order food, recharge, view balance, use emergency balance where enabled.
- **Parent**: monitor child activity, set limits, add funds, receive alerts.

### 4.3 Corporate Roles
- **Employee**: place orders, recharge, view personal ledger.
- **Department Head**: view team or department spend analytics.
- **HR Admin**: manage allowances, subsidies, and employee configuration.

### 4.4 Restaurant Roles
- **Anonymous Customer**: browse via QR and pay per order.
- **Registered Customer**: loyalty, history, and saved preferences.

---

## 5. Wallet and Payment Model

### 5.1 Wallet Rules
- Wallet contains a **main balance** and an **emergency balance entitlement**.
- Orders are only allowed when:
  - `Main Balance + Emergency Available - Blocked Amount >= Order Amount`
- When an order is placed:
  - Amount is **blocked**, not deducted.
- When order is delivered:
  - Amount is **deducted** from main balance first.
  - Emergency balance is consumed only if required.

### 5.2 Emergency Balance
- Configurable per tenant.
- Must have rules for:
  - Eligible roles
  - Maximum monthly usage
  - Recovery method
  - Optional parent notification
- Recovered automatically on the next recharge.

### 5.3 Recharge Channels
- Online payment gateway
- Manual operator recharge
- Cash / cheque where supported
- Bulk subsidy upload for corporate tenants
- Parent-funded recharge for students

### 5.4 Ledger Requirements
Every wallet event must create a transaction record:
- Recharge
- Order block
- Delivery deduction
- Refund
- Emergency recovery
- Manual adjustment

---

## 6. Order Lifecycle

### 6.1 Core State Machine
`Placed -> Confirmed -> Preparing -> Ready -> Delivered -> Completed`

Supporting transitions:
- `Cancelled` when user cancels before preparation
- `Cancelled` when auto-timeout expires
- `Refunded` when a blocked order is released

### 6.2 Order Strategy
**Recommended rule:**
- **Block on order placement**
- **Deduct on delivery**
- **Reserve stock on placement**
- **Release stock on cancellation**

### 6.3 Order Channels
1. **Pre-order**
   - User selects items, chooses time slot, pays from wallet, collects later.

2. **Instant counter order**
   - NFC/QR identifies user at the counter.
   - Operator builds cart and confirms on the spot.

3. **Anonymous QR order**
   - User scans table/venue QR.
   - Pays online or in cash.

### 6.4 Auto-Cancellation
- Ready orders expire after a tenant-configurable time window, typically 30 minutes.
- Expired orders must:
  - Unblock wallet amount
  - Release reserved stock
  - Mark order as cancelled
  - Notify the user
  - Track no-show count

---

## 7. Inventory Model

### 7.1 Inventory States
- `Total Stock`
- `Reserved Stock`
- `Available Stock = Total - Reserved`

### 7.2 Inventory Rules
- Reserve stock when order is placed.
- Deduct stock when order is delivered.
- Release reservation on cancellation.
- Low stock alerts must be tenant-configurable.
- Wastage must be tracked separately from sales.

### 7.3 Wastage Events
- Cancelled after preparation
- Expired items
- Damaged items
- Overproduction / overcooking

### 7.4 Suggested Inventory Outputs
- Daily sales summary
- Wastage report
- Restock alerts
- Low stock dashboard
- Item popularity report

---

## 8. NFC and QR Interaction Model

### 8.1 NFC Workflow
1. User taps card.
2. System identifies user.
3. System displays profile and balance.
4. Operator selects or confirms order.
5. System blocks or deducts as required.
6. Receipt is generated.

### 8.2 NFC Card Management
- Assign / reassign cards
- Activate / deactivate
- Block lost cards
- Support backup cards
- Handle expiry and audit logs

### 8.3 QR Workflow
- QR may represent table, counter, or tenant context.
- Works without login for anonymous customers.
- Must prevent cross-tenant misuse.

---

## 9. Menu and Pricing

### 9.1 Menu Structure
- Categories: breakfast, lunch, dinner, snacks, beverages
- Item types: pre-cooked, made-to-order, combo
- Dietary flags: veg, non-veg, vegan, Jain, allergens
- Nutrition metadata: calories, protein, carbs, fat
- Availability by time slot, day, and quantity

### 9.2 Pricing Model
- Base price
- Role-based price overrides
- Subsidy support
- Dynamic pricing / happy hours
- Tax rules per tenant

### 9.3 Availability Rules
- Schedule-based availability
- Daily limits and stock limits
- Out-of-stock quick toggle
- Hidden items should remain visible to operators when necessary

---

## 10. Offline-First Architecture

### 10.1 Why Offline-First
Canteens cannot stop functioning because the internet is unavailable during peak hours. Order taking, wallet blocking, and delivery confirmation must continue locally.

### 10.2 Local Node Responsibilities
- Local user cache
- Local menu cache
- Local order queue
- Local wallet snapshot
- Local stock reservation
- Local receipt printing
- Background sync to cloud

### 10.3 Sync Principles
- Write locally first.
- Sync asynchronously when online.
- Use retry and conflict resolution.
- Preserve financial integrity above all else.

### 10.4 Conflict Rules
- Wallet conflicts: cloud balance is authoritative, with audit review.
- Stock conflicts: last-write-wins with timestamp and transaction reconciliation.
- Order conflicts: delivery history and transaction log must remain immutable.

---

## 11. Reporting and Analytics

### 11.1 Operational Reports
- Orders by status
- Peak hours
- Ready/pending queue
- Today’s revenue
- No-show count
- Low balance alerts

### 11.2 Financial Reports
- Daily collection
- Refunds
- Outstanding dues
- Subsidy utilization
- Tax reports
- Settlement summaries

### 11.3 Business Insights
- Popular items
- Waste patterns
- Revenue trends
- User spending patterns
- Department spend analytics
- Parent activity summaries

---

## 12. Notifications

The system should support:
- Order confirmed
- Order ready
- Order delivered
- Low balance warning
- Recharge success
- Emergency balance recovery
- Low stock alert
- Promotion / menu update
- Parent alerts for child usage

Preferred channels:
- Push
- SMS
- Email
- WhatsApp where enabled

---

## 13. Security and Compliance

### 13.1 Security Controls
- RBAC enforced on all APIs and UI actions
- Audit logs for financial and administrative actions
- Two-factor authentication for admins
- Encrypted secrets and payment credentials
- No raw card data storage

### 13.2 Compliance Areas
- Food safety and allergen visibility
- Tax and invoice support
- Data privacy and retention rules
- Financial audit trail

---

## 14. Recommended Delivery Phases

### Phase 1: MVP
- Tenant setup
- Educational institute module
- NFC integration
- Wallet and recharge
- Menu and basic ordering
- Reservation-based stock flow

### Phase 2: Expansion
- Corporate module
- Advanced reporting
- Inventory controls
- Parent portal
- Mobile app enhancements

### Phase 3: Restaurant/QSR
- QR ordering
- Anonymous checkout
- Table service support
- Loyalty program

### Phase 4: Optimization
- Predictive inventory
- AI recommendations
- Health and nutrition features
- ERP / HRMS integrations

---

## 15. Principal Architect Decision Summary

### Decisions
- **One platform, configuration-driven multi-tenancy**
- **Reserve on order, deduct on delivery**
- **Emergency balance as controlled safety credit**
- **Offline-first counter experience**
- **Strong RBAC and auditability**
- **Tenant-specific operational rules and branding**

### Non-Negotiables
- No tenant data leakage
- No financial action without audit trail
- No order flow dependency on live internet
- No hard-coded business rules that block future client types

---

## 16. Final Concept Statement

This system is a configurable, multi-tenant canteen platform designed to serve educational institutions, corporate cafeterias, and restaurant/QSR operations from a single product core. The architecture prioritizes operational continuity, financial correctness, tenant isolation, and extensibility. The key business rule is simple: **block first, deliver second, and sync everything with an auditable trail**.
