# Google Books API Integration - User Workflow Design

## **Streamlined Setup Process** 🚀

### **Phase 1: One-Click Project Setup (90% Automated)**
```
┌─────────────────────────────────────────────────────────────────┐
│  ReadAIrr Settings > Metadata Providers > Google Books         │
│                                                                 │
│  [🔗 Setup Google Books API]  ← Single button click            │
│                                                                 │
│  ✨ This will:                                                  │
│  1. Open Google Cloud Console with pre-filled project template │
│  2. Guide you through 3 simple clicks                          │  
│  3. Automatically return with your API credentials             │
└─────────────────────────────────────────────────────────────────┘
```

### **Phase 2: Guided Setup Wizard**
```
Step 1: Google Account Login
┌─────────────────────────────────────────────────────────────────┐
│  [Sign in with Google] ← OAuth popup                           │
│  ✓ Authenticated as user@gmail.com                             │
└─────────────────────────────────────────────────────────────────┘

Step 2: API Project Creation (Semi-automated)
┌─────────────────────────────────────────────────────────────────┐
│  📋 Follow these 3 simple steps:                                │
│                                                                 │
│  1. Click → [Create ReadAIrr Project] (opens pre-filled form)  │
│  2. Click → [Enable Books API] (auto-navigates)               │  
│  3. Copy → API Key (auto-detects and validates)               │
│                                                                 │
│  [📹 Watch 30-second walkthrough video]                        │
└─────────────────────────────────────────────────────────────────┘

Step 3: Automatic Integration
┌─────────────────────────────────────────────────────────────────┐
│  ✓ API Key validated and saved                                  │
│  ✓ Test query successful                                        │
│  ✓ Duration data available for audiobooks                      │
│                                                                 │
│  🎉 Google Books metadata is now active!                       │
└─────────────────────────────────────────────────────────────────┘
```

## **Technical Implementation** 🔧

### **Backend Service Architecture**
```csharp
public class GoogleBooksMetadataProvider : IMetadataProvider
{
    public class GoogleBooksSetupService 
    {
        // Generate pre-filled Google Cloud Console URLs
        public string GenerateProjectSetupUrl(string userEmail)
        {
            return $"https://console.cloud.google.com/projectcreate?" +
                   $"name=readairr-books-{DateTime.Now:yyyyMMdd}&" +
                   $"template=books-api-starter";
        }
        
        // Auto-detect and validate API keys
        public async Task<bool> ValidateAndSaveApiKey(string apiKey)
        {
            var testQuery = await _googleBooksApi.TestQuery(apiKey);
            if (testQuery.Success)
            {
                await _configService.SetGoogleBooksApiKey(apiKey);
                return true;
            }
            return false;
        }
    }
    
    // Audiobook duration extraction
    public async Task<AudioBookMetadata> GetAudioBookData(string isbn)
    {
        var response = await _httpClient.GetAsync(
            $"https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn}");
            
        // Extract duration from volumeInfo.audiobook.duration
        return new AudioBookMetadata 
        {
            Duration = ParseDuration(volume.VolumeInfo?.AudioBook?.Duration),
            Narrators = volume.VolumeInfo?.AudioBook?.Narrators,
            Language = volume.VolumeInfo?.Language
        };
    }
}
```

### **Frontend Wizard Components**
```typescript
// React component for guided setup
export const GoogleBooksSetupWizard = () => {
  const [step, setStep] = useState(1);
  
  const handleGoogleLogin = async () => {
    // OAuth flow with Google
    const authResult = await googleAuth.signIn();
    if (authResult.success) setStep(2);
  };
  
  const generateProjectUrl = () => {
    return `/api/v1/metadata/google-books/setup-url`;
  };
  
  const validateApiKey = async (apiKey: string) => {
    const response = await fetch('/api/v1/metadata/google-books/validate', {
      method: 'POST',
      body: JSON.stringify({ apiKey })
    });
    return response.json();
  };
};
```

## **Enhanced User Experience Features** ✨

### **1. Smart Assistance**
- **Video Tutorial**: Embedded 30-second walkthrough
- **Live Validation**: Real-time API key testing
- **Error Recovery**: Clear troubleshooting for common issues
- **Progress Tracking**: Visual progress bar through setup

### **2. Fallback Options**
```
Primary: Semi-automated Google Setup (recommended)
    ↓ (if user prefers manual setup)
Fallback 1: Manual API key entry with validation
    ↓ (if Google Books unavailable)  
Fallback 2: Alternative duration sources (Audible scraping)
    ↓ (if no external sources)
Fallback 3: Manual duration entry with validation
```

### **3. Ongoing Management**
- **Auto-refresh**: Handles OAuth token renewal
- **Usage Monitoring**: Shows API quota usage
- **Health Checks**: Monitors API availability
- **One-click Re-setup**: Easy re-authentication

## **Privacy & Security** 🔐

### **Data Handling**
```
✓ No Google credentials stored in ReadAIrr
✓ Only API keys (which user controls) are saved
✓ OAuth tokens handled securely with refresh rotation
✓ API usage stays within user's Google project quotas
✓ Clear privacy disclosure about what data is accessed
```

## **Implementation Priority** 📋

### **Phase 1: Core Integration** (Week 1)
- [ ] Google Books API client implementation
- [ ] Duration data extraction and parsing
- [ ] Basic manual API key configuration

### **Phase 2: Guided Setup** (Week 2) 
- [ ] OAuth integration for Google login
- [ ] Wizard UI components
- [ ] Pre-filled project template URLs
- [ ] API key validation service

### **Phase 3: Enhanced UX** (Week 3)
- [ ] Video tutorial integration
- [ ] Smart error handling and recovery
- [ ] Usage monitoring dashboard
- [ ] Automated testing and health checks

## **Expected User Experience** 🎉

**Time to Setup**: ~2-3 minutes (down from 15+ minutes manual)
**Success Rate**: 95%+ (with guided steps)
**Maintenance**: Zero (automated token management)

Users will have a Netflix-like "sign in with Google" experience that just works!
