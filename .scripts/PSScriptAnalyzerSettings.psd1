@{
    # PSScriptAnalyzer settings for Catan project
    # See: https://learn.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/using-scriptanalyzer

    Severity = @('Error', 'Warning')

    ExcludeRules = @(
        # Write-Host is intentional for colored CLI output
        'PSAvoidUsingWriteHost',

        # Empty catch blocks are used intentionally to suppress non-critical errors
        'PSAvoidUsingEmptyCatchBlock',

        # ConvertTo-SecureString with plaintext is required for certificate creation
        # The password is randomly generated and only used for dev certificates
        'PSAvoidUsingConvertToSecureStringWithPlainText',

        # BOM is not required - UTF-8 without BOM is standard for cross-platform
        'PSUseBOMForUnicodeEncodedFile',

        # ShouldProcess not needed for internal helper functions in CLI scripts
        'PSUseShouldProcessForStateChangingFunctions',

        # Plural nouns are appropriate for functions that operate on collections
        'PSUseSingularNouns',

        # Aliases like 'kill' are acceptable in CLI scripts for readability
        'PSAvoidUsingCmdletAliases',

        # Non-standard verbs are used for internal helper functions (Doctor-, Clean-, Ensure-, etc.)
        'PSUseApprovedVerbs',

        # Unused parameters are often kept for API consistency or future use
        'PSReviewUnusedParameter',

        # Unused variables are sometimes intentional (capturing output to suppress it)
        'PSUseDeclaredVarsMoreThanAssignments',

        # Invoke-Expression is sometimes necessary for dynamic script execution
        'PSAvoidUsingInvokeExpression',

        # Overwriting built-in cmdlets is intentional in some wrapper scripts
        'PSAvoidOverwritingBuiltInCmdlets',

        # Null comparison order is a style preference
        'PSPossibleIncorrectComparisonWithNull',

        # Assigning to automatic variables is sometimes intentional
        'PSAvoidAssignmentToAutomaticVariable'
    )
}
