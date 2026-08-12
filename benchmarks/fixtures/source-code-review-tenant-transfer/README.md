# Review target

Review `TransferService.cs` for concrete correctness, security, and reliability defects. Do not modify it.

Contract:

- both accounts must belong to the supplied tenant;
- transfer amounts are strictly positive;
- debit, credit, and audit are one all-or-nothing operation;
- caller cancellation propagates through every asynchronous dependency;
- logs never contain credentials or idempotency secrets.
