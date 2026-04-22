# E-Wallet System - Architecture Documentation

## 📐 System Architecture Overview

This document provides a detailed overview of the E-Wallet system's architecture, design patterns, and implementation details.

## Layers & Components

### 1. API Layer (`src/API/`)

**Responsibility**: HTTP request/response handling and routing

**Components**:
- **Controllers** (`Controllers/`)
  - `AuthController.cs` - Authentication endpoints
  - `WalletController.cs` - Wallet operations endpoints

- **Middleware** (`Middleware/`)
  - `GlobalExceptionMiddleware.cs` - Centralized exception handling

**Flow**:
```
HTTP Request → Controller → Service → Repository → Database
     ↓
Response Filter/Exception Middleware ← Error
     ↓
HTTP Response
```

### 2. Application Layer (`src/Application/`)

**Responsibility**: Business logic, data transformation, and interfaces

**Components**:

#### DTOs (Data Transfer Objects)
- **AuthDtos.cs**: Registration, login, token responses
- **WalletDtos.cs**: Deposit, withdraw, transfer, transaction responses
- **ApiResponse.cs**: Generic API response wrapper

#### Services (Business Logic)
- **IAuthService / AuthService.cs**
  - User registration with validation
  - User authentication with JWT tokens
  - Password hashing with BCrypt

- **IWalletService / WalletService.cs**
  - Balance management
  - Deposit operations with transaction locking
  - Withdrawal operations with balance validation
  - Money transfers with atomic operations
  - Transaction history retrieval
  - Cache invalidation strategy

- **ICacheService / CacheService.cs**
  - Redis cache (production)
  - In-memory cache (development/fallback)

#### Repositories (Data Access Interfaces)
- **IUserRepository**: User CRUD operations
- **ITransactionRepository**: Transaction queries and persistence

#### Validators (Input Validation)
- **AuthValidators.cs**: Email format, password strength
- **WalletValidators.cs**: Amount validation (> 0)

#### Exceptions (Custom Error Types)
- `WalletException` - Base exception
- `InsufficientFundsException`
- `UserNotFoundException`
- `DuplicateEmailException`
- `InvalidCredentialsException`
- `ConcurrencyException`

### 3. Domain Layer (`src/Domain/`)

**Responsibility**: Business entities and core business rules

**Components**:

#### Entities
- **User.cs**
  ```csharp
  - Id (PK)
  - Email (Unique)
  - PasswordHash
  - Balance (Decimal)
  - Version (Concurrency Token)
  - CreatedAt / UpdatedAt
  - Navigation: Sent/Received Transactions
  ```

- **Transaction.cs**
  ```csharp
  - Id (PK)
  - FromUserId (FK, Nullable)
  - ToUserId (FK, Nullable)
  - Amount
  - Type (Deposit/Withdraw/Transfer)
  - Status (Pending/Success/Failed)
  - IdempotencyKey (Unique, Nullable)
  - Description
  - CreatedAt / UpdatedAt
  ```

### 4. Infrastructure Layer (`src/Infrastructure/`)

**Responsibility**: Data persistence, external services, and technical implementations

**Components**:

#### Data (`Data/`)
- **WalletDbContext.cs**
  - Entity Framework Core configuration
  - Database model mapping
  - Relationship configurations
  - Index definitions

#### Repositories (`Repositories/`)
- **UserRepository.cs**
  - Implements IUserRepository
  - User queries and persistence
  - Password hash updates

- **TransactionRepository.cs**
  - Implements ITransactionRepository
  - Transaction queries
  - Idempotency key lookups
  - Pagination support

#### Security (`Security/`)
- **JwtTokenService.cs**
  - JWT token generation
  - Claims configuration
  - Token expiration handling

#### Caching (`Caching/`)
- **RedisCacheService.cs** - Redis-backed cache
- **InMemoryCacheService.cs** - In-memory fallback cache

#### Migrations (`Migrations/`)
- **20240101000000_InitialCreate.cs** - Database schema creation
- Migration snapshots and designer files

---

## 🔄 Data Flow Examples

### Registration Flow

```
POST /api/auth/register
    ↓
AuthController.Register()
    ↓
FluentValidation (Email, Password strength)
    ↓
IAuthService.RegisterAsync()
    ↓
Check duplicate email (IUserRepository.GetByEmailAsync)
    ↓
Hash password (BCrypt)
    ↓
Create User entity
    ↓
IUserRepository.CreateAsync()
    ↓
Generate JWT token (IJwtTokenService)
    ↓
Return AuthTokenResponse
    ↓
200/201 Response
```

### Transfer Flow (Most Complex)

```
POST /api/wallet/transfer
    ↓
WalletController.Transfer()
    ↓
Extract UserId from JWT claims
    ↓
FluentValidation (Amount > 0, ToUserId not empty)
    ↓
IWalletService.TransferAsync()
    ↓
├─ Validate recipient exists
├─ Check idempotency key (prevent duplicates)
├─ Verify sender has sufficient funds
│
├─ BEGIN TRANSACTION
│   ├─ Lock sender row (WITH UPDLOCK, ROWLOCK)
│   ├─ Lock recipient row (WITH UPDLOCK, ROWLOCK)
│   ├─ Double-check sender balance
│   ├─ Deduct from sender (Version++)
│   ├─ Add to recipient (Version++)
│   ├─ Create Transaction record
│   └─ COMMIT / ROLLBACK
│
├─ Invalidate cache for both users
├─ Return TransactionResponse
│
200 Response
```

### Concurrency Handling

```
Multiple requests attempt to transfer from same account:

Request 1: SELECT user FOR UPDATE
           Balance: $1000, Version: 1

Request 2: SELECT user FOR UPDATE (waits for Request 1)

Request 1: Transfers $500
           Balance: $500, Version: 2
           COMMIT (releases lock)

Request 2: Gets lock
           Balance: $500, Version: 2
           Checks: $500 >= $600? NO
           ERROR: Insufficient funds
           ROLLBACK
```

---

## 🔒 Security Architecture

### Authentication & Authorization

```
┌─────────────────────────────────────┐
│        Unauthenticated Request      │
└──────────────────┬──────────────────┘
                   │
        ┌──────────▼──────────┐
        │  /api/auth/register │
        │  /api/auth/login    │
        └─────────────────────┘
                   │
        ┌──────────▼────────────────────┐
        │   JWT Token Generated         │
        │   Exp: 24 hours               │
        └──────────────────┬────────────┘
                           │
┌──────────────────────────▼──────────────────────────┐
│        Authenticated Request with Token             │
│        Authorization: Bearer <token>                │
└──────────────────────────┬──────────────────────────┘
                           │
        ┌──────────────────▼──────────────────┐
        │  [Authorize] Attribute Validation  │
        │  - Token signature verification   │
        │  - Expiration check                │
        │  - Claim extraction                │
        └──────────────────┬──────────────────┘
                           │
                ┌──────────▼──────────┐
                │  Access Granted     │
                │  /api/wallet/*      │
                └─────────────────────┘
```

### Concurrency Control Strategy

#### 1. **Row-Level Locking (Pessimistic)**
```sql
SELECT * FROM Users WHERE Id = @userId 
WITH (UPDLOCK, ROWLOCK)
```
- **Pros**: Prevents race conditions absolutely
- **Cons**: Blocks other transactions, lower concurrency
- **Used for**: Critical money operations

#### 2. **Optimistic Concurrency (Version Field)**
```csharp
public int Version { get; set; } // Concurrency token

// EF Core detects if Version changed
user.Balance = 900;
user.Version++; // Increment version
await _context.SaveChangesAsync(); // Throws if Version doesn't match
```
- **Pros**: Higher concurrency, better performance
- **Cons**: Retry logic needed on conflicts
- **Used for**: Balance updates

#### 3. **Idempotency Keys**
```csharp
public string? IdempotencyKey { get; set; }

// Unique constraint prevents duplicate transactions
CREATE UNIQUE INDEX IX_IdempotencyKey 
    ON Transactions([IdempotencyKey]) 
    WHERE [IdempotencyKey] IS NOT NULL
```
- **Pros**: Prevents duplicate processing
- **Cons**: Requires client to generate
- **Used for**: Duplicate request detection

---

## 💾 Database Design

### Tables & Relationships

```
┌──────────────────────────────────────┐
│ Users                                │
├──────────────────────────────────────┤
│ Id (GUID, PK)                        │
│ Email (VARCHAR(256), Unique)         │
│ PasswordHash (VARCHAR(MAX))          │
│ Balance (NUMERIC(18,2))              │
│ Version (INT, Concurrency Token)     │
│ CreatedAt (DATETIME2)                │
│ UpdatedAt (DATETIME2)                │
└──────────────────────────────────────┘
        ▲              ▲
        │              │
        │ FromUserId   │ ToUserId
        │ (FK)         │ (FK)
        │              │
        └──────────┬───┘
                   │
    ┌──────────────▼──────────────────┐
    │ Transactions                     │
    ├──────────────────────────────────┤
    │ Id (GUID, PK)                    │
    │ FromUserId (GUID, FK, Nullable)  │
    │ ToUserId (GUID, FK, Nullable)    │
    │ Amount (NUMERIC(18,2))           │
    │ Type (INT: 0=Deposit, 1=Withdraw,│
    │       2=Transfer)                │
    │ Status (INT: 0=Pending,          │
    │         1=Success, 2=Failed)     │
    │ IdempotencyKey (VARCHAR(100),    │
    │                 Unique, Nullable)│
    │ Description (VARCHAR(MAX))       │
    │ CreatedAt (DATETIME2)            │
    │ UpdatedAt (DATETIME2)            │
    └──────────────────────────────────┘
```

### Indexes

```
Users:
├─ PK_Users (Id)
└─ IX_Users_Email (Email) UNIQUE

Transactions:
├─ PK_Transactions (Id)
├─ IX_Transactions_FromUserId (FromUserId)
├─ IX_Transactions_ToUserId (ToUserId)
├─ IX_Transactions_CreatedAt (CreatedAt)
└─ IX_Transactions_IdempotencyKey (IdempotencyKey) 
   WHERE [IdempotencyKey] IS NOT NULL UNIQUE
```

---

## 🧵 Dependency Injection

### Service Registration (Program.cs)

```csharp
// Database
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseSqlServer(connectionString));

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults...)
    .AddJwtBearer(options => {...});

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Caching
if (redisEnabled)
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
else
    builder.Services.AddScoped<ICacheService, InMemoryCacheService>();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining(typeof(RegisterRequestValidator));
```

**Dependency Graph**:
```
WalletController
├─ IWalletService (WalletService)
│  ├─ IUserRepository (UserRepository)
│  │  └─ WalletDbContext
│  ├─ ITransactionRepository (TransactionRepository)
│  │  └─ WalletDbContext
│  ├─ ICacheService (RedisCacheService or InMemoryCacheService)
│  └─ ILogger
└─ ILogger
```

---

## 🔍 Error Handling

### Exception Hierarchy

```
Exception
└─ WalletException
   ├─ InsufficientFundsException
   ├─ UserNotFoundException
   ├─ DuplicateEmailException
   ├─ InvalidCredentialsException
   ├─ ConcurrencyException
   └─ InvalidOperationException
```

### Middleware Exception Mapping

```
Exception Type                    → HTTP Status Code
────────────────────────────────────────────────────
UserNotFoundException              → 404 Not Found
InsufficientFundsException         → 400 Bad Request
DuplicateEmailException            → 409 Conflict
InvalidCredentialsException        → 401 Unauthorized
ConcurrencyException               → 409 Conflict
InvalidOperationException          → 400 Bad Request
WalletException (generic)          → 400 Bad Request
Other Exception                    → 500 Internal Server Error
```

---

## 📊 Performance Considerations

### Caching Strategy

```
GET /api/wallet/balance
    │
    ├─ Check cache ("balance_<userId>")
    │  ├─ Hit: Return cached response (5-min TTL)
    │  └─ Miss: Continue to database
    │
    ├─ Query database
    │
    └─ Cache for 5 minutes
```

### Query Optimization

- Index on frequently queried columns
- Efficient pagination with Skip/Take
- Lazy loading avoided (explicit Include)
- Async I/O throughout

### Lock Contention Minimization

```
Instead of:
SELECT * FROM Users FOR UPDATE  (holds lock)
SELECT * FROM Transactions      (still holding)

Do:
SELECT * FROM Users FOR UPDATE  (get lock, update, release)
SELECT * FROM Transactions      (new lock if needed)
```

---

## 🚀 Scalability Considerations

### Horizontal Scaling

```
Load Balancer
├─ Instance 1
│  └─ In-Memory Cache (not shared)
├─ Instance 2
│  └─ In-Memory Cache (not shared)
└─ Instance 3
   └─ In-Memory Cache (not shared)

Solution: Use Shared Redis Cache
├─ Instance 1 ──┐
├─ Instance 2 ──┼─── Redis (Shared)
└─ Instance 3 ──┘
```

### Database Scaling

- **Read replicas**: For high read volume (transactions history)
- **Connection pooling**: Min 10, Max 100 connections
- **Partitioning**: By user ID for very large tables
- **Archival**: Move old transactions to archive table

### Rate Limiting

Add middleware:
```csharp
app.UseRateLimiter(new RateLimiterOptions
{
    RateLimiters = new Dictionary<string, RateLimiter>
    {
        ["fixed"] = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromSeconds(60)
        })
    }
});
```

---

## 📋 Design Patterns Used

### 1. **Repository Pattern**
```csharp
public interface IUserRepository { ... }
public class UserRepository : IUserRepository { ... }
```
✓ Abstraction of data access logic
✓ Testability
✓ Easy to swap implementations

### 2. **Service Layer Pattern**
```csharp
public interface IWalletService { ... }
public class WalletService : IWalletService { ... }
```
✓ Centralized business logic
✓ Reusability across controllers
✓ Transaction management

### 3. **Dependency Injection**
✓ Loose coupling
✓ Testability
✓ Flexibility in implementations

### 4. **Data Transfer Objects (DTOs)**
```csharp
public class DepositRequest { ... }
public class TransactionResponse { ... }
```
✓ Input/output contract definition
✓ Validation at API layer
✓ Security (no direct entity exposure)

### 5. **Global Exception Middleware**
✓ Consistent error responses
✓ Centralized error handling
✓ Logging integration

### 6. **Unit of Work (Database Transactions)**
✓ Atomic operations
✓ Rollback on failure
✓ Data consistency

---

## 🧪 Testing Strategy

### Unit Tests (Service Layer)
```csharp
[Test]
public async Task Transfer_WithInsufficientFunds_ThrowsException()
{
    // Arrange
    var sender = new User { Balance = 100 };

    // Act & Assert
    Assert.ThrowsAsync<InsufficientFundsException>(
        () => walletService.TransferAsync(senderId, new TransferRequest { Amount = 500 })
    );
}
```

### Integration Tests
```csharp
[Test]
public async Task TransferTransaction_IsAtomic()
{
    // Full flow with database
    // Verify both users updated or neither
}
```

### Load Tests
```
100 concurrent transfers from same account
Expected: Only 1 succeeds, others rejected
Verify: No double-spending
```

---

## 📚 References

- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [ASP.NET Core Security](https://docs.microsoft.com/en-us/aspnet/core/security/)
- [JWT Authentication](https://tools.ietf.org/html/rfc7519)

