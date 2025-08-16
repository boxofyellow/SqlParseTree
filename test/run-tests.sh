#!/bin/bash

# Test runner script for SqlParseTree
# This script runs all .sql files in the test directory and compares the output
# with the corresponding expected files (with different extensions)

set -e  # Exit on any error

echo "🧪 Running SqlParseTree tests..."

# Build the project first
echo "🔨 Building project..."
dotnet build --configuration Release

# Initialize test counters
total_tests=0
passed_tests=0
failed_tests=0

# Create a temporary directory for output files
temp_dir=$(mktemp -d)
trap "rm -rf $temp_dir" EXIT

# Find and test all SQL files with their corresponding expected output files
for sql_file in test/*.sql; do
    if [ -f "$sql_file" ]; then
        base_name=$(basename "$sql_file" .sql)
        
        # Find all files that match the base name with different extensions
        found_tests=false
        
        for expected_file in test/${base_name}.*; do
            if [ -f "$expected_file" ] && [ "$expected_file" != "$sql_file" ]; then
                # Extract the extension and use it as the format
                extension="${expected_file##*.}"
                format="$extension"
                found_tests=true
                
                echo ""
                echo "📝 Testing: $base_name (format: $format)"
                total_tests=$((total_tests + 1))
                
                # Create temporary output file with appropriate extension
                temp_output="$temp_dir/${base_name}_output.$extension"
                
                # Run the application with the SQL file as input and save to temp file
                if cat "$sql_file" | dotnet run --no-build --configuration Release -- --format "$format" --to-file --output-path "$temp_output" --log-destination StdError; then
                    # Compare the files directly
                    if cmp -s "$temp_output" "$expected_file"; then
                        echo "✅ PASS: $base_name ($format)"
                        passed_tests=$((passed_tests + 1))
                    else
                        echo "❌ FAIL: $base_name ($format)"
                        echo ""
                        echo "Expected (from $expected_file):"
                        cat "$expected_file"
                        echo ""
                        echo "Actual (from application output):"
                        cat "$temp_output"
                        echo ""
                        echo "Differences:"
                        diff "$expected_file" "$temp_output" || true
                        echo ""
                        failed_tests=$((failed_tests + 1))
                    fi
                else
                    echo "❌ FAIL: $base_name ($format) (application error)"
                    echo "Failed to run application with input: $sql_file"
                    failed_tests=$((failed_tests + 1))
                fi
            fi
        done
        
        if [ "$found_tests" = false ]; then
            echo "⚠️  Warning: No expected output files found for $sql_file"
            echo "      Looking for files matching: ${base_name}.*"
        fi
    fi
done

echo ""
echo "📊 Test Results:"
echo "   Total tests: $total_tests"
echo "   Passed: $passed_tests"
echo "   Failed: $failed_tests"

if [ $failed_tests -eq 0 ] && [ $total_tests -gt 0 ]; then
    echo "🎉 All tests passed!"
    exit 0
elif [ $total_tests -eq 0 ]; then
    echo "⚠️  No tests found!"
    exit 1
else
    echo "💥 Some tests failed!"
    exit 1
fi
