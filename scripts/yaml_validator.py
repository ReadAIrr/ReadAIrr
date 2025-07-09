#!/usr/bin/env python3
"""Simple YAML syntax validator for Docker Compose files"""

import sys
import re

def validate_yaml_basic(filepath):
    """Basic YAML validation without external dependencies"""
    
    try:
        with open(filepath, 'r') as f:
            lines = f.readlines()
    except Exception as e:
        print(f"Error reading file: {e}")
        return False
    
    errors = []
    line_num = 0
    
    for line in lines:
        line_num += 1
        stripped = line.strip()
        
        # Skip empty lines and comments
        if not stripped or stripped.startswith('#'):
            continue
            
        # Check for basic YAML structure issues
        
        # Check for tabs (YAML should use spaces)
        if '\t' in line:
            errors.append(f"Line {line_num}: Contains tabs (use spaces in YAML)")
        
        # Check for obvious syntax errors
        if ':' in stripped and not stripped.startswith('-'):
            # Check if colon is followed by space or end of line
            # Skip environment variable assignments (lines starting with -)
            colon_pos = stripped.find(':')
            if colon_pos < len(stripped) - 1:
                next_char = stripped[colon_pos + 1]
                if next_char not in [' ', '\n', '\r'] and not stripped.endswith(':'):
                    # Allow environment variable syntax like "VAR=value"
                    if '=' not in stripped[colon_pos:]:
                        errors.append(f"Line {line_num}: Missing space after colon")
        
        # Check for unmatched quotes
        quote_count = stripped.count('"') + stripped.count("'")
        if quote_count % 2 != 0:
            errors.append(f"Line {line_num}: Unmatched quotes")
    
    if errors:
        print("YAML syntax issues found:")
        for error in errors:
            print(f"  {error}")
        return False
    else:
        print("Basic YAML syntax appears valid")
        return True

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python3 yaml_validator.py <file.yml>")
        sys.exit(1)
    
    filepath = sys.argv[1]
    if validate_yaml_basic(filepath):
        sys.exit(0)
    else:
        sys.exit(1)
