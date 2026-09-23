# Secure ServiceDesk Access --- Manual API Test Guide

This document records the manual Postman flow for the **Secure
ServiceDesk Access** feature.

## Prerequisites

-   Run the ServiceDesk API locally.
-   Base URL used in this guide:

``` text
http://localhost:5011
```

-   Bootstrap the first Administrator if the installation is empty.
-   If `run-local.ps1` generates a temporary JWT signing key, restarting
    the API invalidates JWTs issued by the previous run.

------------------------------------------------------------------------

## 1. Administrator Login

**Method:** `POST`

**Endpoint:**

``` text
http://localhost:5011/api/auth/login
```

**Authorization:** No Auth

**Body → raw → JSON:**

``` json
{
  "email": "<administrator-email>",
  "password": "<administrator-password>"
}
```

**Expected response:** `200 OK`

Example response:

``` json
{
  "accessToken": "<ADMIN_ACCESS_TOKEN>",
  "expiresAt": "<EXPIRATION_DATE>"
}
```

Keep the Administrator `accessToken` for the protected requests below.

------------------------------------------------------------------------

## 2. Create a Customer User

**Method:** `POST`

**Endpoint:**

``` text
http://localhost:5011/api/users
```

**Authorization:** Bearer Token

Use:

``` text
<ADMIN_ACCESS_TOKEN>
```

**Body → raw → JSON:**

``` json
{
  "firstName": "Test",
  "lastName": "Customer",
  "email": "customer@test.com",
  "role": 0
}
```

> `role: 0` represents `Customer` in the current HTTP request model.

**Expected response:** `201 Created`

Keep the returned Customer `id`:

``` text
<CUSTOMER_ID>
```

------------------------------------------------------------------------

## 3. Provision Customer Access

**Method:** `POST`

**Endpoint:**

``` text
http://localhost:5011/api/users/<CUSTOMER_ID>/access-provisioning
```

**Authorization:** Bearer Token

Use:

``` text
<ADMIN_ACCESS_TOKEN>
```

**Body:** None

**Expected response:** `201 Created`

Example response:

``` json
{
  "activationToken": "<ACTIVATION_TOKEN>",
  "expiresAt": "<EXPIRATION_DATE>"
}
```

The activation token is returned only for provisioning and should be
treated as sensitive. Keep it for the activation request.

------------------------------------------------------------------------

## 4. Activate the Customer Account

**Method:** `POST`

**Endpoint:**

``` text
http://localhost:5011/api/auth/activate
```

**Authorization:** No Auth

**Body → raw → JSON:**

``` json
{
  "activationToken": "<ACTIVATION_TOKEN>",
  "password": "<CUSTOMER_PASSWORD>"
}
```

The password must satisfy the ServiceDesk password policy. In the
current implementation, it must contain **15--128 Unicode code points**.

**Expected response:** `204 No Content`

An empty response body is expected.

------------------------------------------------------------------------

## 5. Customer Login

**Method:** `POST`

**Endpoint:**

``` text
http://localhost:5011/api/auth/login
```

**Authorization:** No Auth

**Body → raw → JSON:**

``` json
{
  "email": "customer@test.com",
  "password": "<CUSTOMER_PASSWORD>"
}
```

**Expected response:** `200 OK`

Example response:

``` json
{
  "accessToken": "<CUSTOMER_ACCESS_TOKEN>",
  "expiresAt": "<EXPIRATION_DATE>"
}
```

------------------------------------------------------------------------

## Complete Access Flow

``` text
Bootstrap Administrator
        ↓
Administrator Login
        ↓
Create Customer
        ↓
Provision Customer Access
        ↓
Customer Activates Account
        ↓
Customer Login
```

A successful Customer login confirms that the complete manual access
flow is working.
