# Bank Withdrawal Code Improvement Exercise

## Overview

This repository contains my implementation for the **Bank Account
Withdrawal Code Improvement Exercise**.

The supplied implementation was written in Java/Spring. My first step
was to understand and map the technologies used in the original solution
to their .NET equivalents. I then reviewed the withdrawal flow with a
focus on concurrency, transactional consistency, separation of
responsibilities, dependency management and reliable event publishing.

I chose to implement my proposed solution in **C# / ASP.NET Core** and
run it end to end, even though a working implementation was not
required.

The main objective was to preserve the original withdrawal capability
while addressing the failure and concurrency scenarios identified during
the review.

## Technology Stack

-   C#
-   ASP.NET Core Web API
-   SQL Server / LocalDB
-   Microsoft.Data.SqlClient
-   AWS SDK for .NET
-   Amazon SNS
-   System.Text.Json

## Solution Approach

-   Atomic conditional SQL update instead of a separate
    read/check/update.
-   Validation for zero or negative withdrawal amounts.
-   `Withdrawals` audit table.
-   Transactional Outbox Pattern using `OutboxMessages`.
-   Account update, withdrawal record and outbox event committed in the
    same SQL transaction.
-   Background `OutboxDispatcher` for pending events.
-   `WithdrawalCompleted` events published to AWS SNS.
-   `System.Text.Json` serialization instead of manual JSON
    construction.
-   Separation of HTTP, application, persistence and messaging
    responsibilities.
-   ASP.NET Core dependency injection.
-   Cancellation token propagation.
-   `WithdrawalId` and `EventId` values for traceability.

## Architecture

``` text
POST /api/accounts/{accountId}/withdrawals
                  |
                  v
       WithdrawalsController
                  |
                  v
         WithdrawalService
                  |
                  v
        IAccountRepository
                  |
      +-----------+-----------+
      |     SQL TRANSACTION   |
      |                       |
      |  1. Update Account    |
      |  2. Insert Withdrawal |
      |  3. Insert Outbox     |
      |                       |
      +-----------+-----------+
                  |
                COMMIT
                  |
                  v
          OutboxMessages
                  |
                  v
          OutboxDispatcher
                  |
                  v
          IEventPublisher
                  |
                  v
              AWS SNS
                  |
                  v
             Subscriber
```

If any database operation fails before commit, the account update,
withdrawal record and outbox message are rolled back together. SNS
publishing happens **after** the banking transaction has completed.

## Atomic Withdrawal

``` sql
UPDATE Accounts
SET Balance = Balance - @Amount
WHERE Id = @AccountId
  AND Balance >= @Amount;
```

If exactly one row is affected, the withdrawal succeeds. If no row is
affected, the account does not exist or has insufficient funds.

## Transactional Outbox

A successful withdrawal writes to:

-   `Accounts` --- balance updated.
-   `Withdrawals` --- withdrawal audit recorded.
-   `OutboxMessages` --- `WithdrawalCompleted` event stored.

All three database operations execute inside **one database
transaction**.

After commit, `OutboxDispatcher` retrieves unpublished messages and
sends them to SNS. Once publishing succeeds, `PublishedAtUtc` is
recorded. An SNS failure can therefore be retried without executing the
withdrawal again.

## Project Structure

``` text
BankWithdrawal.Api
|
+-- Application
|   +-- Models
|   +-- Services
|
+-- BackgroundServices
|   +-- OutboxDispatcher.cs
|
+-- Contracts
|   +-- WithdrawalRequest.cs
|
+-- Controllers
|   +-- WithdrawalsController.cs
|
+-- Infrastructure
|   +-- Messaging
|   |   +-- IEventPublisher.cs
|   |   +-- SnsEventPublisher.cs
|   |
|   +-- Outbox
|   |   +-- IOutboxRepository.cs
|   |   +-- OutboxRepository.cs
|   |
|   +-- IAccountRepository.cs
|   +-- AccountRepository.cs
|
+-- appsettings.json
+-- Program.cs
```

## Database

The implementation requires:

``` text
Accounts
Withdrawals
OutboxMessages
```

## Configuration

``` json
{
  "ConnectionStrings": {
    "DefaultConnection": "<SQL Server connection string>"
  },
  "AWS": {
    "Region": "<AWS region>",
    "SnsTopicArn": "<SNS topic ARN>"
  }
}
```

### AWS Credentials

AWS credentials are **not stored in the repository**.

For local development, configure credentials through an appropriate AWS
credential provider such as an AWS profile or environment variables.

The application IAM identity only requires permission to publish to the
assessment SNS topic:

``` text
sns:Publish
```

## Running the API

``` bash
dotnet restore
dotnet build
dotnet run
```

## Example Request

``` http
POST /api/accounts/123/withdrawals
Content-Type: application/json
```

``` json
{
  "amount": 500
}
```

## End-to-End Flow

``` text
HTTP Request
     |
     v
Validate Amount
     |
     v
Atomic Account Update
     |
     v
Insert Withdrawal
     |
     v
Insert Outbox Message
     |
     v
COMMIT
     |
     v
API Response

OutboxDispatcher
     |
     v
Find Unpublished Messages
     |
     v
AWS SNS
     |
     v
Subscriber
     |
     v
Mark PublishedAtUtc
```

## Validation Performed

I validated:

-   Successful withdrawal
-   Insufficient balance behaviour
-   Invalid withdrawal amount handling
-   Database transaction rollback
-   Withdrawal audit creation
-   Outbox message creation
-   Background outbox processing
-   AWS SNS publishing
-   Subscriber delivery
-   `PublishedAtUtc` update after successful publication

The final end-to-end test used a real AWS SNS Standard topic and
successfully delivered the `WithdrawalCompleted` event to a subscriber.

## Further Improvements

Given more time, I would add:

-   Automated concurrency tests
-   Integration tests around the complete transaction
-   Dispatcher retry/backoff strategy
-   Dead-letter handling for repeatedly failing outbox messages
-   Stronger downstream idempotency/deduplication
-   Metrics and alerting for failed or ageing outbox messages
-   Additional structured logging and correlation identifiers

## Assessment Document

The accompanying assessment document contains the full code review,
reasoning behind the changes, implementation decisions and end-to-end
evidence.

------------------------------------------------------------------------

**Prepared by Thabang Makume**\
**Bank Account Withdrawal --- Code Improvement Exercise**
