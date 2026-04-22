#!/bin/bash
# E-Wallet API - Example curl commands

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
API_URL="https://localhost:7001"
INSECURE="-k" # For self-signed certificates in development

echo -e "${BLUE}E-Wallet API - Curl Examples${NC}\n"

# ========================================
# AUTHENTICATION ENDPOINTS
# ========================================
echo -e "${YELLOW}=== AUTHENTICATION ===${NC}\n"

# Register User 1
echo -e "${GREEN}1. Register User 1${NC}"
RESPONSE=$(curl $INSECURE -s -X POST "$API_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "alice@example.com",
    "password": "AlicePassword123",
    "confirmPassword": "AlicePassword123"
  }')
echo "$RESPONSE" | jq .
USER1_ID=$(echo "$RESPONSE" | jq -r '.data.userId')
TOKEN1=$(echo "$RESPONSE" | jq -r '.data.token')
echo -e "User ID: $USER1_ID"
echo -e "Token: ${TOKEN1:0:50}...\n"

# Register User 2
echo -e "${GREEN}2. Register User 2${NC}"
RESPONSE=$(curl $INSECURE -s -X POST "$API_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "bob@example.com",
    "password": "BobPassword123",
    "confirmPassword": "BobPassword123"
  }')
echo "$RESPONSE" | jq .
USER2_ID=$(echo "$RESPONSE" | jq -r '.data.userId')
TOKEN2=$(echo "$RESPONSE" | jq -r '.data.token')
echo -e "User ID: $USER2_ID\n"

# Login
echo -e "${GREEN}3. Login${NC}"
RESPONSE=$(curl $INSECURE -s -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "alice@example.com",
    "password": "AlicePassword123"
  }')
echo "$RESPONSE" | jq .
echo ""

# ========================================
# WALLET ENDPOINTS
# ========================================
echo -e "${YELLOW}=== WALLET OPERATIONS ===${NC}\n"

# Get Initial Balance
echo -e "${GREEN}4. Get Balance (should be 0)${NC}"
curl $INSECURE -s -X GET "$API_URL/api/wallet/balance" \
  -H "Authorization: Bearer $TOKEN1" | jq .
echo ""

# Deposit Money
echo -e "${GREEN}5. Deposit 1000 to Alice${NC}"
DEPOSIT_RESPONSE=$(curl $INSECURE -s -X POST "$API_URL/api/wallet/deposit" \
  -H "Authorization: Bearer $TOKEN1" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 1000,
    "description": "Initial deposit from salary",
    "idempotencyKey": "alice-deposit-001"
  }')
echo "$DEPOSIT_RESPONSE" | jq .
DEPOSIT_ID=$(echo "$DEPOSIT_RESPONSE" | jq -r '.data.id')
echo ""

# Verify Balance After Deposit
echo -e "${GREEN}6. Verify Alice Balance After Deposit${NC}"
curl $INSECURE -s -X GET "$API_URL/api/wallet/balance" \
  -H "Authorization: Bearer $TOKEN1" | jq .
echo ""

# Deposit to User 2
echo -e "${GREEN}7. Deposit 500 to Bob${NC}"
curl $INSECURE -s -X POST "$API_URL/api/wallet/deposit" \
  -H "Authorization: Bearer $TOKEN2" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 500,
    "description": "Initial deposit",
    "idempotencyKey": "bob-deposit-001"
  }' | jq .
echo ""

# Withdraw Money
echo -e "${GREEN}8. Alice Withdraws 100${NC}"
WITHDRAW_RESPONSE=$(curl $INSECURE -s -X POST "$API_URL/api/wallet/withdraw" \
  -H "Authorization: Bearer $TOKEN1" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 100,
    "description": "ATM Withdrawal",
    "idempotencyKey": "alice-withdraw-001"
  }')
echo "$WITHDRAW_RESPONSE" | jq .
echo ""

# Verify Balance After Withdrawal
echo -e "${GREEN}9. Verify Alice Balance After Withdrawal${NC}"
curl $INSECURE -s -X GET "$API_URL/api/wallet/balance" \
  -H "Authorization: Bearer $TOKEN1" | jq .
echo ""

# Transfer Money
echo -e "${GREEN}10. Alice Transfers 250 to Bob${NC}"
TRANSFER_RESPONSE=$(curl $INSECURE -s -X POST "$API_URL/api/wallet/transfer" \
  -H "Authorization: Bearer $TOKEN1" \
  -H "Content-Type: application/json" \
  -d "{
    \"toUserId\": \"$USER2_ID\",
    \"amount\": 250,
    \"description\": \"Payment for services\",
    \"idempotencyKey\": \"alice-to-bob-001\"
  }")
echo "$TRANSFER_RESPONSE" | jq .
echo ""

# Check Alice Balance After Transfer
echo -e "${GREEN}11. Verify Alice Balance After Transfer${NC}"
ALICE_FINAL=$(curl $INSECURE -s -X GET "$API_URL/api/wallet/balance" \
  -H "Authorization: Bearer $TOKEN1" | jq '.data.balance')
echo "Alice Final Balance: $ALICE_FINAL"
echo ""

# Check Bob Balance After Receiving Transfer
echo -e "${GREEN}12. Verify Bob Balance After Receiving Transfer${NC}"
BOB_FINAL=$(curl $INSECURE -s -X GET "$API_URL/api/wallet/balance" \
  -H "Authorization: Bearer $TOKEN2" | jq '.data.balance')
echo "Bob Final Balance: $BOB_FINAL"
echo ""

# Get Transaction History
echo -e "${GREEN}13. Get Alice Transaction History${NC}"
curl $INSECURE -s -X GET "$API_URL/api/wallet/transactions?page=1&pageSize=50" \
  -H "Authorization: Bearer $TOKEN1" | jq .
echo ""

echo -e "${GREEN}14. Get Bob Transaction History${NC}"
curl $INSECURE -s -X GET "$API_URL/api/wallet/transactions?page=1&pageSize=50" \
  -H "Authorization: Bearer $TOKEN2" | jq .
echo ""

# ========================================
# ERROR CASES
# ========================================
echo -e "${YELLOW}=== ERROR CASES ===${NC}\n"

# Insufficient Funds
echo -e "${GREEN}15. Try to Withdraw More Than Balance (should fail)${NC}"
curl $INSECURE -s -X POST "$API_URL/api/wallet/withdraw" \
  -H "Authorization: Bearer $TOKEN2" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 10000,
    "description": "Attempt to overdraw",
    "idempotencyKey": "bob-overdraw-001"
  }' | jq .
echo ""

# Duplicate Email
echo -e "${GREEN}16. Try to Register with Duplicate Email (should fail)${NC}"
curl $INSECURE -s -X POST "$API_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "alice@example.com",
    "password": "DifferentPassword123",
    "confirmPassword": "DifferentPassword123"
  }' | jq .
echo ""

# Invalid Email Format
echo -e "${GREEN}17. Try to Register with Invalid Email (should fail)${NC}"
curl $INSECURE -s -X POST "$API_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "invalid-email",
    "password": "ValidPassword123",
    "confirmPassword": "ValidPassword123"
  }' | jq .
echo ""

# Weak Password
echo -e "${GREEN}18. Try to Register with Weak Password (should fail)${NC}"
curl $INSECURE -s -X POST "$API_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "charlie@example.com",
    "password": "weak",
    "confirmPassword": "weak"
  }' | jq .
echo ""

# Transfer to Self
echo -e "${GREEN}19. Try to Transfer to Self (should fail)${NC}"
curl $INSECURE -s -X POST "$API_URL/api/wallet/transfer" \
  -H "Authorization: Bearer $TOKEN1" \
  -H "Content-Type: application/json" \
  -d "{
    \"toUserId\": \"$USER1_ID\",
    \"amount\": 100,
    \"description\": \"Self transfer\",
    \"idempotencyKey\": \"alice-self-transfer\"
  }" | jq .
echo ""

# Invalid Recipient
echo -e "${GREEN}20. Try to Transfer to Non-Existent User (should fail)${NC}"
curl $INSECURE -s -X POST "$API_URL/api/wallet/transfer" \
  -H "Authorization: Bearer $TOKEN1" \
  -H "Content-Type: application/json" \
  -d '{
    "toUserId": "00000000-0000-0000-0000-000000000000",
    "amount": 100,
    "description": "Transfer to invalid user",
    "idempotencyKey": "alice-invalid-user"
  }' | jq .
echo ""

# ========================================
# IDEMPOTENCY TESTS
# ========================================
echo -e "${YELLOW}=== IDEMPOTENCY TESTS ===${NC}\n"

echo -e "${GREEN}21. First Deposit with Idempotency Key${NC}"
IDEMPOTENT_KEY="idempotent-deposit-$(date +%s)"
RESPONSE1=$(curl $INSECURE -s -X POST "$API_URL/api/wallet/deposit" \
  -H "Authorization: Bearer $TOKEN1" \
  -H "Content-Type: application/json" \
  -d "{
    \"amount\": 50,
    \"description\": \"Test idempotency\",
    \"idempotencyKey\": \"$IDEMPOTENT_KEY\"
  }")
echo "$RESPONSE1" | jq .
FIRST_TRANSACTION_ID=$(echo "$RESPONSE1" | jq -r '.data.id')
echo ""

echo -e "${GREEN}22. Repeat Same Request with Same Idempotency Key (should return same transaction)${NC}"
RESPONSE2=$(curl $INSECURE -s -X POST "$API_URL/api/wallet/deposit" \
  -H "Authorization: Bearer $TOKEN1" \
  -H "Content-Type: application/json" \
  -d "{
    \"amount\": 50,
    \"description\": \"Test idempotency\",
    \"idempotencyKey\": \"$IDEMPOTENT_KEY\"
  }")
echo "$RESPONSE2" | jq .
SECOND_TRANSACTION_ID=$(echo "$RESPONSE2" | jq -r '.data.id')

if [ "$FIRST_TRANSACTION_ID" == "$SECOND_TRANSACTION_ID" ]; then
  echo -e "${GREEN}✓ Idempotency working correctly - same transaction returned${NC}"
else
  echo -e "${YELLOW}✗ Idempotency issue - different transactions returned${NC}"
fi
echo ""

# ========================================
# SUMMARY
# ========================================
echo -e "${BLUE}=== SUMMARY ===${NC}"
echo "Total users created: 2"
echo "Alice (ID: $USER1_ID)"
echo "  - Initial balance: 0"
echo "  - After deposit: 1000"
echo "  - After withdrawal: 900"
echo "  - After transfer: 650"
echo ""
echo "Bob (ID: $USER2_ID)"
echo "  - Initial balance: 0"
echo "  - After deposit: 500"
echo "  - After receiving transfer: 750"
echo ""

# Notes
echo -e "${YELLOW}NOTES:${NC}"
echo "- Replace 'localhost' with actual API URL for remote deployment"
echo "- Remove '-k' flag when using valid HTTPS certificates"
echo "- Generate your own UUIDs if needed instead of using test IDs"
echo "- Idempotency keys should be unique per request"
echo "- All timestamps are in UTC"
echo ""
