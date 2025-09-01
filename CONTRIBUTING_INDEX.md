# ReadAIrr Index Usage Guide for Contributors

This guide explains how to effectively use ReadAIrr's project index system to improve your development productivity and code navigation experience.

## Table of Contents
- [Index Overview](#index-overview)
- [Local Index Generation](#local-index-generation)
- [Editor Configuration](#editor-configuration)
- [Code Search Techniques](#code-search-techniques)
- [Index Structure](#index-structure)
- [Advanced Usage](#advanced-usage)
- [Troubleshooting](#troubleshooting)

## Index Overview

ReadAIrr maintains comprehensive code indexes to accelerate development workflows:

- **Backend Index** (`tools/index/backend/`) - C# symbol navigation and metadata
- **Frontend Index** (`tools/index/frontend/`) - TypeScript/JavaScript navigation and quality metrics  
- **Database Index** (`tools/index/database/`) - Schema documentation and entity relationships
- **API Index** (`tools/index/api/`) - REST endpoints and OpenAPI specifications

### Benefits
- **Instant Symbol Navigation** - Jump to definitions across the entire codebase
- **Cross-Reference Discovery** - Find all usages of classes, methods, and variables
- **Code Quality Insights** - Access linting results and quality metrics
- **Architecture Understanding** - Browse database schema and API structure
- **Offline Development** - Work efficiently without internet connectivity

## Local Index Generation

### Quick Start

```bash
# Generate all indexes
make index

# Generate specific index types
make index-backend     # C# code symbols
make index-frontend    # TypeScript/JavaScript symbols  
make index-database    # Database schema
make index-api         # API endpoints
```

### Prerequisites

Install required tools for optimal indexing:

```bash
# macOS (via Homebrew)
brew install universal-ctags

# Ubuntu/Debian
sudo apt-get install universal-ctags

# Windows (via Chocolatey)
choco install universal-ctags
```

### Advanced Index Options

```bash
# Clean and regenerate all indexes
make clean-index && make index

# Generate with summary documentation
make index-summary

# Check index generation status
make status
```

## Editor Configuration

### Visual Studio Code

#### Required Extensions

Install these extensions for optimal ReadAIrr development:

```json
{
  "recommendations": [
    "ms-dotnettools.csharp",
    "ms-dotnettools.vscode-dotnet-runtime", 
    "bradlc.vscode-tailwindcss",
    "ms-vscode.vscode-typescript-next",
    "formulahendry.auto-rename-tag",
    "christian-kohler.path-intellisense",
    "ms-vscode.vscode-json",
    "redhat.vscode-yaml"
  ]
}
```

#### Workspace Settings

Create `.vscode/settings.json`:

```json
{
  "dotnet.defaultSolution": "src/Readarr.sln",
  "typescript.preferences.includePackageJsonAutoImports": "auto",
  "typescript.suggest.autoImports": true,
  "typescript.updateImportsOnFileMove.enabled": "always",
  "eslint.workingDirectories": ["frontend"],
  "css.validate": true,
  "less.validate": true,
  "scss.validate": true,
  "omnisharp.enableEditorConfigSupport": true,
  "omnisharp.enableImportCompletion": true,
  "omnisharp.enableMSBuildLoadProjectsOnDemand": true,
  "search.exclude": {
    "**/node_modules": true,
    "**/bower_components": true,
    "**/build": true,
    "**/dist": true,
    "**/bin": true,
    "**/obj": true,
    "**/.git": true
  },
  "files.watcherExclude": {
    "**/node_modules/**": true,
    "**/build/**": true,
    "**/dist/**": true,
    "**/bin/**": true,
    "**/obj/**": true
  }
}
```

#### Tasks Configuration

Create `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Generate Indexes",
      "type": "shell",
      "command": "make",
      "args": ["index"],
      "group": "build",
      "presentation": {
        "echo": true,
        "reveal": "always",
        "focus": false,
        "panel": "shared"
      },
      "problemMatcher": []
    },
    {
      "label": "Build All",
      "type": "shell", 
      "command": "make",
      "args": ["build"],
      "group": {
        "kind": "build",
        "isDefault": true
      },
      "presentation": {
        "echo": true,
        "reveal": "always",
        "focus": false,
        "panel": "shared"
      }
    },
    {
      "label": "Run Tests",
      "type": "shell",
      "command": "make", 
      "args": ["test"],
      "group": "test",
      "presentation": {
        "echo": true,
        "reveal": "always",
        "focus": false,
        "panel": "shared"
      }
    }
  ]
}
```

### Vim/Neovim

#### Basic Configuration

Add to your `.vimrc` or `init.vim`:

```vim
" ReadAIrr tag file locations
set tags+=./tools/index/backend/tags.backend
set tags+=./tools/index/frontend/tags.frontend

" Enhanced tag jumping
nnoremap <C-]> g<C-]>
nnoremap g<C-]> <C-]>

" Tag navigation shortcuts
nnoremap <leader>] :tag<space>
nnoremap <leader>[ :pop<CR>
nnoremap <leader>} :tnext<CR>
nnoremap <leader>{ :tprevious<CR>

" Search in project
nnoremap <leader>/ :grep -r<space>

" ReadAIrr specific file types
autocmd BufNewFile,BufRead *.cs setfiletype cs
autocmd BufNewFile,BufRead *.tsx setfiletype typescript.tsx
autocmd BufNewFile,BufRead *.jsx setfiletype javascript.jsx
```

#### Advanced Configuration with fzf

```vim
" Fuzzy tag search
nnoremap <leader>t :Tags<CR>
nnoremap <leader>T :BTags<CR>

" Fuzzy file search within ReadAIrr directories
nnoremap <leader>f :Files<CR>
nnoremap <leader>F :GFiles<CR>

" Search in ReadAIrr source
nnoremap <leader>r :Rg<space>

" ReadAIrr project shortcuts
nnoremap <leader>ps :Files src/<CR>
nnoremap <leader>pf :Files frontend/src/<CR>
nnoremap <leader>pd :Files docs/<CR>
```

### Emacs

#### Basic Configuration

Add to your Emacs configuration:

```elisp
;; ReadAIrr tag tables
(setq tags-table-list
      '("./tools/index/backend/tags.backend"
        "./tools/index/frontend/tags.frontend"))

;; Auto-revert tags tables
(setq tags-revert-without-query t)

;; Enhanced tag navigation
(global-set-key (kbd "M-.") 'find-tag)
(global-set-key (kbd "M-,") 'pop-tag-mark)
(global-set-key (kbd "M-?") 'tags-apropos)

;; Project-wide search
(global-set-key (kbd "C-c s") 'grep-find)
```

#### With Projectile

```elisp
;; ReadAIrr project configuration
(require 'projectile)
(projectile-mode +1)
(define-key projectile-mode-map (kbd "C-c p") 'projectile-command-map)

;; Project-specific settings
(dir-locals-set-class-variables
 'readairr-project
 '((nil . ((projectile-project-compilation-cmd . "make build")
          (projectile-project-test-cmd . "make test")
          (projectile-project-run-cmd . "make docker-up")))))

(dir-locals-set-directory-class
 (expand-file-name "~/path/to/readairr") 'readairr-project)
```

### IntelliJ IDEA / Rider

#### Project Settings

1. **Open ReadAIrr Solution**
   - File → Open → Select `src/Readarr.sln`

2. **Configure Code Style**
   - Settings → Editor → Code Style → C# → Import from `src/.editorconfig`
   - Settings → Editor → Code Style → TypeScript → Set to match project conventions

3. **Enable External Tools**
   - Settings → Tools → External Tools → Add:
     - Name: "Generate Indexes"
     - Program: `make`
     - Arguments: `index`
     - Working Directory: `$ProjectFileDir$`

#### Useful Plugins

- **C# Support** - Enhanced C# language support
- **TypeScript** - Advanced TypeScript features
- **Database Tools** - SQL and database schema navigation
- **.env files support** - Environment variable management

## Code Search Techniques

### Command Line Search with ripgrep

Install ripgrep for blazing-fast code search:

```bash
# macOS
brew install ripgrep

# Ubuntu/Debian
sudo apt-get install ripgrep

# Windows
choco install ripgrep
```

#### Search Examples

```bash
# Find all references to a class
rg "BookController" --type cs

# Search for API endpoints
rg "\[Http(Get|Post|Put|Delete)\]" --type cs

# Find React components
rg "export.*component" --type ts --type tsx

# Search for database entities
rg "class.*: IEntity" --type cs

# Find SignalR message handlers
rg "handle[A-Z].*=" --type js --type ts

# Search for TODO comments
rg "TODO|FIXME|HACK" --type cs --type ts --type js

# Find configuration settings
rg "appsettings|config\." --type json --type cs

# Search for error handling
rg "try\s*{|catch\s*\(" --type cs --type ts
```

#### Advanced ripgrep Usage

```bash
# Search with context lines
rg "IBookService" -A 3 -B 3

# Search only in specific directories
rg "useEffect" frontend/src/

# Exclude certain patterns
rg "console\.log" --glob "!*test*" --glob "!*spec*"

# Search for regex patterns
rg "Author.*Controller" --type cs

# Output only file names
rg "BookFile" --files-with-matches

# Search in specific file types
rg "interface.*Resource" --include "*.cs" --include "*.ts"
```

### Using fzf for Fuzzy Search

Install fzf for interactive file and content search:

```bash
# macOS
brew install fzf

# Ubuntu/Debian
sudo apt-get install fzf

# Windows
choco install fzf
```

#### fzf Integration Examples

```bash
# Fuzzy file search
find . -type f -name "*.cs" -o -name "*.ts" -o -name "*.tsx" | fzf

# Search through git files
git ls-files | fzf

# Interactive grep
rg --line-number --no-heading . | fzf --delimiter : --preview 'bat --color=always --highlight-line {2} {1}'

# Search through tags
cat tools/index/backend/tags.backend | fzf

# Find and edit files
vim $(fzf)
```

### Symbol Navigation with CTags

#### Basic CTags Commands

```bash
# Jump to definition
:tag SymbolName

# Show all matching tags  
:tselect SymbolName

# Navigate tag stack
:tnext          # Next tag
:tprevious      # Previous tag
:tfirst         # First tag
:tlast          # Last tag

# Pop from tag stack
:pop

# List all tags in current file
:tags
```

#### Advanced CTags Usage

```bash
# Find all references to a symbol
grep -n "YourSymbol" tools/index/backend/tags.backend

# Search for specific tag types
grep -E "^.*\tc\t" tools/index/backend/tags.backend    # Classes
grep -E "^.*\tm\t" tools/index/backend/tags.backend    # Methods
grep -E "^.*\tf\t" tools/index/frontend/tags.frontend  # Functions

# Find tags by filename
grep "BookController.cs" tools/index/backend/tags.backend

# Search with partial matches
grep -i "book.*service" tools/index/backend/tags.backend
```

## Index Structure

### Backend Index (`tools/index/backend/`)

| File | Description | Usage |
|------|-------------|-------|
| `tags.backend` | CTags index for C# code | Symbol navigation, go-to-definition |
| `symbols.md` | Human-readable symbol summary | Code review, architecture overview |

### Frontend Index (`tools/index/frontend/`)

| File | Description | Usage |
|------|-------------|-------|
| `tags.frontend` | CTags index for TS/JS code | Symbol navigation, refactoring |
| `quality.md` | Code quality report | Code review, technical debt tracking |
| `.eslintcache` | ESLint cache for performance | Faster linting on subsequent runs |
| `eslint-results.json` | Detailed lint results | Automated quality analysis |
| `*.tsbuildinfo` | TypeScript build information | Incremental compilation |

### Database Index (`tools/index/database/`)

| File | Description | Usage |
|------|-------------|-------|
| `index.md` | Schema overview and statistics | Database architecture review |

### API Index (`tools/index/api/`)

| File | Description | Usage |
|------|-------------|-------|
| `endpoints.md` | Controller and endpoint list | API reference, integration planning |
| `openapi.json` | OpenAPI 3.0 specification | API documentation, client generation |

## Advanced Usage

### Custom Index Generation

Create custom index scripts for specific workflows:

```bash
#!/bin/bash
# custom-index.sh - Generate focused index for specific feature

FEATURE_DIR="$1"

if [ -z "$FEATURE_DIR" ]; then
    echo "Usage: $0 <feature-directory>"
    exit 1
fi

# Generate focused tags for specific feature
ctags -R \
    --languages=C#,TypeScript,JavaScript \
    --exclude=bin --exclude=obj --exclude=node_modules \
    -f "tools/index/custom-${FEATURE_DIR}.tags" \
    "src/${FEATURE_DIR}/" "frontend/src/components/${FEATURE_DIR}/"

echo "Custom index generated: tools/index/custom-${FEATURE_DIR}.tags"
```

### Integration with External Tools

#### Sourcegraph Integration

For larger codebases, integrate with Sourcegraph:

```bash
# Install Sourcegraph CLI
curl -L https://sourcegraph.com/.api/src-cli/src_linux_amd64 -o /usr/local/bin/src
chmod +x /usr/local/bin/src

# Search across ReadAIrr codebase
src search "BookService" -repo="readairr"
```

#### GitHub Codespaces

Configure Codespaces for ReadAIrr development:

```json
// .devcontainer/devcontainer.json
{
  "name": "ReadAIrr Development",
  "image": "mcr.microsoft.com/vscode/devcontainers/dotnet:6.0",
  "features": {
    "ghcr.io/devcontainers/features/node:1": {
      "version": "18"
    }
  },
  "postCreateCommand": "make setup",
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-dotnettools.csharp",
        "ms-vscode.vscode-typescript-next"
      ]
    }
  }
}
```

## Troubleshooting

### Common Issues

#### CTags Not Found

**Problem**: `ctags` command not available or using wrong version.

**Solution**:
```bash
# Check CTags version (should be Universal CTags)
ctags --version

# macOS: Replace outdated ctags
brew install --HEAD universal-ctags

# Ubuntu: Install universal-ctags
sudo apt-get install universal-ctags
```

#### Incomplete Index Generation

**Problem**: Missing symbols or incomplete tag files.

**Solution**:
```bash
# Clean and regenerate
make clean-index
make index

# Check for build errors
make build-backend
make build-frontend

# Verify source directories exist
ls -la src/
ls -la frontend/src/
```

#### Editor Not Using Indexes

**Problem**: IDE not recognizing tag files.

**Solutions**:

**VS Code**: Restart OmniSharp
```
Ctrl+Shift+P → "OmniSharp: Restart OmniSharp"
```

**Vim**: Reload tags
```vim
:set tags?
:set tags+=./tools/index/backend/tags.backend
```

**Emacs**: Reload tag tables
```elisp
M-x visit-tags-table RET ./tools/index/backend/tags.backend RET
```

#### Performance Issues

**Problem**: Slow indexing or search performance.

**Solutions**:
```bash
# Use parallel processing
make -j$(nproc) index

# Exclude large directories
export CTAGS_EXCLUDE="--exclude=node_modules --exclude=build --exclude=dist"

# Use ripgrep instead of grep
alias grep='rg'
```

### Getting Help

1. **Check the Makefile**: `make help` shows all available commands
2. **Review logs**: Check CI workflow logs for indexing examples
3. **GitHub Issues**: Search existing issues or create new ones
4. **Discord/Slack**: Join the ReadAIrr development community

### Contributing to Index System

To improve the indexing system:

1. **Fork the repository**
2. **Modify index generation scripts** in `.github/workflows/index.yml` or `Makefile`
3. **Test locally** with `make index`
4. **Submit pull request** with clear description of improvements

## Best Practices

### Daily Development Workflow

```bash
# Morning routine - refresh indexes
git pull origin develop
make index

# During development - incremental updates
make index-backend    # After modifying C# code
make index-frontend   # After modifying TS/JS code

# Before committing - verify quality
make lint
make test
```

### Team Collaboration

- **Share index configurations** - Commit `.vscode/settings.json` and editor configs
- **Document custom workflows** - Add team-specific search patterns
- **Maintain index freshness** - Use automated GitHub Actions workflow
- **Report index issues** - Help improve the system for everyone

This comprehensive guide should help you maximize productivity when contributing to ReadAIrr. The index system is designed to grow with the project—as you discover new workflows or techniques, consider contributing them back to help the entire development team!
