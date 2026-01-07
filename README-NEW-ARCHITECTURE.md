# Dynamic E-Canteen Management System

## 🎯 Overview

A comprehensive, multi-tenant canteen management system built with .NET 8, supporting Educational Institutes, Corporate Companies, and Restaurants. Features offline-first architecture, NFC card integration, emergency balance system, and real-time inventory management.

## 🏗️ Architecture

### 3-Tier Architecture

```
┌─────────────────────────────────────┐
│   Presentation Layer                │
│   - Controllers (API & MVC)          │
│   - Views (Razor Pages)              │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│   Application Layer                 │
│   - Services (Business Logic)        │
│   - DTOs (Data Transfer Objects)    │
│   - Interfaces                       │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│   Infrastructure Layer               │
│   - Data Access (EF Core)           │
│   - Repositories                     │
│   - External Services                │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│   Domain Layer                       │
│   - Entities                         │
│   - Value Objects                    │
└─────────────────────────────────────┘
```

## 📁 Project Structure

```
CanteenManagementSystem/
├── Domain/
│   └── Entities/          # Domain models
│       ├── Client.cs
│       ├── User.cs
│       ├── UserBalance.cs
│       ├── Order.cs
│       ├── MenuItem.cs
│       └── ...
│
├── Application/
│   ├── DTOs/              # Data Transfer Objects
│   ├── Interfaces/        # Service interfaces
│   └── Services/          # Business logic services
│
├── Infrastructure/
│   ├── Data/              # DbContext
│   └── Repositories/      # Data access
│
├── Controllers/
│   ├── Api/              # REST API controllers
│   └── ...               # MVC controllers
│
└── Views/                # Razor views
```

## 🚀 Key Features

### 1. Multi-Tenant Architecture
- Support for multiple clients (Educational, Corporate, Restaurant)
- Client-specific configuration
- Isolated data per tenant

### 2. Wallet System
- Prepaid wallet with emergency balance
- Automatic emergency balance recovery on recharge
- Transaction ledger
- Role-based pricing

### 3. NFC Card Integration
- Card assignment and management
- NFC-based authentication
- Lost card blocking
- Multi-card support

### 4. Order Management
- Pre-ordering with time slots
- Instant ordering
- Anonymous ordering (Restaurant mode)
- Order status tracking
- Auto-cancellation for unpicked orders

### 5. Inventory Management
- Stock reservation on order
- Stock deduction on delivery
- Wastage tracking
- Low stock alerts
- Real-time stock updates

### 6. Offline Support
- Local database for offline operations
- Background sync service
- Conflict resolution
- Queue-based transaction processing

## 📊 Database Schema

### Core Tables

- **Clients** - Multi-tenant configuration
- **Users** - User accounts with roles
- **UsersWallet** - Wallet balances
- **Transactions** - Transaction ledger
- **NfcCards** - NFC card management
- **MenuCategories** - Menu categories
- **MenuItems** - Menu items with pricing
- **Orders** - Order management
- **OrderItems** - Order line items
- **Inventory** - Stock management
- **InventoryTransactions** - Stock transaction log
- **WastageLog** - Food wastage tracking

## 🔧 Setup Instructions

### 1. Database Setup

Run the migration script:
```sql
-- Execute DB-Scripts-New-Schema.sql
```

### 2. Configuration

Update `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Your connection string"
  },
  "PaymentGateway": {
    "BaseUrl": "https://pgw.cloudcampus24.com",
    "MerchantId": "Your merchant ID",
    "ApiKey": "Your API key"
  }
}
```

### 3. Build and Run

```bash
dotnet restore
dotnet build
dotnet run
```

## 📡 API Endpoints

### Orders
- `POST /api/v1/order` - Place order
- `GET /api/v1/order/{orderId}` - Get order details
- `GET /api/v1/order/user/{userId}` - Get user orders
- `DELETE /api/v1/order/{orderId}` - Cancel order
- `POST /api/v1/order/{orderId}/deliver` - Confirm delivery
- `POST /api/v1/order/{orderId}/ready` - Mark order ready

### Wallet
- `GET /api/v1/wallet/{userId}` - Get wallet balance
- `GET /api/v1/wallet/{userId}/transactions` - Get transactions
- `POST /api/v1/wallet/{userId}/recharge` - Initiate recharge
- `POST /api/v1/wallet/recharge/verify` - Verify recharge

### Menu
- `GET /api/v1/menu/{clientId}` - Get menu
- `GET /api/v1/menu/items/{itemId}` - Get item details
- `PATCH /api/v1/menu/items/{itemId}/availability` - Update availability

### NFC
- `POST /api/v1/nfc/authenticate` - Authenticate card
- `POST /api/v1/nfc/assign` - Assign card to user
- `POST /api/v1/nfc/block` - Block card

## 🎨 Design Principles

### SOLID Principles
- **S**ingle Responsibility - Each service has one responsibility
- **O**pen/Closed - Open for extension, closed for modification
- **L**iskov Substitution - Interfaces properly implemented
- **I**nterface Segregation - Focused interfaces
- **D**ependency Inversion - Depend on abstractions

### DRY (Don't Repeat Yourself)
- Reusable services and components
- Shared DTOs and utilities
- Common error handling

### KISS (Keep It Simple, Stupid)
- Clear, readable code
- Simple architecture
- Easy to understand and maintain

## 🔐 Security

- Role-based access control (RBAC)
- Password hashing (BCrypt)
- SQL injection prevention (EF Core)
- Input validation
- CORS configuration

## 📝 Next Steps

1. **Offline Sync Service** - Implement background sync for offline operations
2. **NFC Reader Integration** - Hardware integration for NFC readers
3. **Receipt Printing** - Thermal printer integration
4. **Mobile App** - React Native or Flutter app
5. **Real-time Updates** - SignalR for live order updates
6. **Analytics Dashboard** - Business intelligence and reporting

## 📚 Documentation

- API Documentation: See `Controllers/Api/` for endpoint details
- Database Schema: See `DB-Scripts-New-Schema.sql`
- Service Documentation: See `Application/Services/` for business logic

## 🤝 Contributing

This is a production-ready system. Follow SOLID, DRY, and KISS principles when contributing.

## 📄 License

Proprietary - All rights reserved

