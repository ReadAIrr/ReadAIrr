# GitHub Copilot Instructions for ReadAIrr Project

## Project Overview

ReadAIrr is a respectful continuation of the original Readarr project - an eBook and audiobook collection manager. This project maintains the core functionality while adding modern improvements and optional AI-enhanced features.

## Core Principles

### 1. Respect for Original Project
- Always maintain attribution to the original Readarr team
- Preserve the legacy and functionality of the original codebase
- Keep compatibility with existing Readarr setups where possible
- Honor the open-source nature and community-driven development

### 2. Developer Privacy and Anonymity
- **DO NOT** reveal or reference the personal identity of the current maintainer/developer
- Refer to the development team generically as "ReadAIrr team" or "ReadAIrr maintainers"
- Keep focus on the project and community rather than individual contributors
- Avoid personal details, names, or identifying information in code comments or documentation
- Use generic commit messages and authorship when suggested

### 3. AI Features Philosophy
- **AI features are OPTIONAL by default** - no AI functionality is active without explicit user enablement
- **Privacy-first approach** - users must opt-in to any AI features
- **Transparent data handling** - clearly document what data AI features access
- **Local processing preferred** - when possible, AI features should work locally
- **User control** - users can disable AI features at any time
- **No assumptions** - never assume users want AI features enabled

## Development Guidelines

### AI Feature Implementation
When building AI-enhanced features:

```markdown
✅ DO:
- Make AI features opt-in only
- Provide clear privacy documentation
- Offer local processing options when possible
- Include toggle switches to disable AI features
- Clearly label AI-powered functionality
- Provide fallback non-AI alternatives
- Document data usage and retention policies

❌ DON'T:
- Enable AI features by default
- Send user data to external services without explicit consent
- Assume users want AI functionality
- Hide AI processing from users
- Make AI features mandatory for core functionality
```

### Code Style and Comments
- Use generic, professional language in code comments
- Focus on functionality rather than personal opinions
- Maintain consistency with existing Readarr code patterns
- Document AI features with extra detail about privacy implications

### Documentation
- Emphasize that ReadAIrr works fully without AI features
- Clearly separate core functionality from AI enhancements
- Provide opt-out instructions for all AI features
- Include privacy policy sections for AI functionality

## AI Feature Categories

### Acceptable AI Features (with proper safeguards)
- **Smart Library Organization** (local analysis only, opt-in)
- **Metadata Enhancement** (user-controlled, with data source transparency)
- **Reading Recommendations** (based on local library only, unless user opts into external)
- **Automated Quality Scoring** (local processing, user can disable)
- **Smart Search** (local indexing enhanced, opt-in for external)

### Required Safeguards for Each AI Feature
1. **Explicit opt-in** during setup or first use
2. **Clear privacy notice** explaining data usage
3. **Local processing option** when technically feasible
4. **Easy disable mechanism** in settings
5. **Fallback functionality** that works without AI
6. **Data retention controls** for any stored AI-related data

## Communication Guidelines

### Public Communications
- Use "we" or "the ReadAIrr team" for collective voice
- Focus on community and project benefits
- Maintain professional, welcoming tone
- Emphasize continuity with original Readarr project

### Issue Responses and Documentation
- Be helpful but maintain project focus over personal identity
- Direct personal questions to general project channels
- Keep responses professional and project-oriented

## Technical Implementation Notes

### Configuration Structure
```yaml
# Example AI feature configuration
ai_features:
  enabled: false  # ALWAYS default to false
  features:
    smart_organization:
      enabled: false
      local_only: true
    metadata_enhancement:
      enabled: false
      external_sources: []
    recommendations:
      enabled: false
      use_external_data: false
```

### Code Examples
```javascript
// Good: AI feature with proper safeguards
if (userSettings.aiFeatures.enabled && userSettings.aiFeatures.smartOrganization.enabled) {
    // Only run AI feature if explicitly enabled
    await enhanceLibraryOrganization();
} else {
    // Always provide non-AI fallback
    await standardLibraryOrganization();
}
```

## Commit Message Guidelines

Use generic, project-focused commit messages:
- ✅ "Add optional AI-powered book recommendations"
- ✅ "Implement privacy-first metadata enhancement"
- ✅ "Update AI feature documentation with opt-out instructions"
- ❌ Personal references or individual attribution

## Documentation Templates

### AI Feature Documentation Template
```markdown
## [Feature Name] (AI-Enhanced, Optional)

**Privacy Status**: This feature is disabled by default and requires explicit user activation.

**Data Usage**: [Clearly describe what data is accessed, processed, or stored]

**Local Processing**: [Yes/No - explain what runs locally vs. externally]

**How to Enable**: [Step-by-step opt-in process]

**How to Disable**: [Step-by-step opt-out process]

**Fallback**: This feature has a non-AI alternative that provides [describe basic functionality].
```

## Testing AI Features

### Required Test Cases
- Feature works when disabled (default state)
- Feature can be enabled and disabled multiple times
- Non-AI fallback functions correctly
- Privacy settings are respected
- Data is not sent externally without permission
- Local processing works offline

## Community Guidelines

### Issue Templates and Responses
- Always clarify whether issues relate to AI features or core functionality
- Provide non-AI solutions first, AI-enhanced solutions as optional additions
- Respect user privacy concerns about AI features
- Never enable AI features to "fix" reported issues

## Legal and Compliance

### Privacy Considerations
- Document all AI feature data usage
- Provide clear opt-out mechanisms
- Respect user data sovereignty
- Comply with privacy regulations (GDPR, CCPA, etc.)
- Regular privacy impact assessments for new AI features

---

*These instructions help maintain ReadAIrr as a privacy-respecting, user-controlled continuation of the Readarr project while enabling optional AI enhancements for users who choose them.*
