# GitHub Secrets Configuration Guide

This guide will help you configure the required GitHub secrets for your CI/CD pipeline to work properly.

## Required Secrets

You need to configure the following secrets in your GitHub repository:

### 1. JWT_SECRET
- **Description**: Your JWT secret key for token validation
- **Current Value**: `b1VUEuyvOSrGwBzozpTdhO10Syy7uakqYBQEFRkwPjMMdAlCRMqQdM9jFHpImznJH46a4JV7ILE9r9TQUOXRsA==`
- **Source**: From your current `docker-compose.yml`

### 2. JWT_ISSUER
- **Description**: Your Supabase JWT issuer URL
- **Current Value**: `https://cyecesagvggsxmrfryse.supabase.co/auth/v1`
- **Source**: From your current `docker-compose.yml`

### 3. JWT_AUDIENCE
- **Description**: Your JWT audience identifier
- **Current Value**: `authenticated`
- **Source**: From your current `docker-compose.yml`

### 4. NEXT_PUBLIC_SUPABASE_URL
- **Description**: Your Supabase project URL
- **Current Value**: `https://cyecesagvggsxmrfryse.supabase.co`
- **Source**: From your JWT issuer URL (remove `/auth/v1`)

### 5. NEXT_PUBLIC_SUPABASE_ANON_KEY
- **Description**: Your Supabase anonymous key
- **How to get**: 
  1. Go to your Supabase project dashboard
  2. Navigate to Settings → API
  3. Copy the "anon public" key

## How to Configure GitHub Secrets

### Step 1: Navigate to Your Repository
1. Go to your GitHub repository: `https://github.com/abdelrahmanelsayed/online-bookstore`
2. Click on the **Settings** tab (at the top of the repository page)

### Step 2: Access Secrets Section
1. In the left sidebar, click on **Secrets and variables**
2. Click on **Actions**

### Step 3: Add Each Secret
For each secret listed above:

1. Click **New repository secret**
2. Enter the **Name** exactly as shown (case-sensitive)
3. Enter the **Secret value**
4. Click **Add secret**

### Step 4: Verify Your Secrets
You should have these 5 secrets configured:
- ✅ JWT_SECRET
- ✅ JWT_ISSUER  
- ✅ JWT_AUDIENCE
- ✅ NEXT_PUBLIC_SUPABASE_URL
- ✅ NEXT_PUBLIC_SUPABASE_ANON_KEY

## Security Notes

⚠️ **Important Security Considerations:**

1. **Never commit secrets to your code** - Always use GitHub secrets
2. **Rotate secrets regularly** - Update your JWT secret periodically
3. **Limit access** - Only repository admins should manage secrets
4. **Monitor usage** - Check GitHub Actions logs for any secret access issues

## Testing Your Configuration

After configuring the secrets:

1. **Push a change** to your `main` or `develop` branch
2. **Check GitHub Actions** tab to see if the pipeline runs successfully
3. **Verify builds** - Both frontend and backend should build without errors
4. **Check deployments** - Services should deploy successfully

## Troubleshooting

### Common Issues:

**❌ "Secret not found" error:**
- Verify the secret name is exactly correct (case-sensitive)
- Check that you added the secret to the correct repository

**❌ "JWT validation failed" error:**
- Verify your JWT_SECRET matches your Supabase configuration
- Check that JWT_ISSUER and JWT_AUDIENCE are correct

**❌ "Supabase connection failed" error:**
- Verify NEXT_PUBLIC_SUPABASE_URL is correct
- Check that NEXT_PUBLIC_SUPABASE_ANON_KEY is valid

### Getting Supabase Keys:

1. **Go to your Supabase project**: https://supabase.com/dashboard
2. **Select your project**
3. **Go to Settings → API**
4. **Copy the required keys**:
   - Project URL → NEXT_PUBLIC_SUPABASE_URL
   - anon public key → NEXT_PUBLIC_SUPABASE_ANON_KEY

## Next Steps

Once you've configured all secrets:

1. ✅ **Test the pipeline** by pushing a commit
2. ✅ **Monitor the build** in GitHub Actions
3. ✅ **Check deployment** status
4. ✅ **Verify health endpoints** are working

Your CI/CD pipeline should now be fully functional! 🚀

## Quick Reference

```bash
# Secret names (exact case-sensitive names):
JWT_SECRET
JWT_ISSUER
JWT_AUDIENCE
NEXT_PUBLIC_SUPABASE_URL
NEXT_PUBLIC_SUPABASE_ANON_KEY

# Current values from your docker-compose.yml:
JWT_SECRET=b1VUEuyvOSrGwBzozpTdhO10Syy7uakqYBQEFRkwPjMMdAlCRMqQdM9jFHpImznJH46a4JV7ILE9r9TQUOXRsA==
JWT_ISSUER=https://cyecesagvggsxmrfryse.supabase.co/auth/v1
JWT_AUDIENCE=authenticated
NEXT_PUBLIC_SUPABASE_URL=https://cyecesagvggsxmrfryse.supabase.co
```

**Note**: You still need to get the `NEXT_PUBLIC_SUPABASE_ANON_KEY` from your Supabase dashboard as this is not stored in your current configuration files.
