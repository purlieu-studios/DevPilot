"""
EmailValidator - A robust email validation class

This class provides comprehensive email validation including:
- Format validation
- Regex pattern matching
- Domain structure validation
- Length checks
- Special character handling
"""

import re
from typing import Tuple, List


class EmailValidator:
    """
    A class to validate email addresses according to RFC 5322 standards.
    """

    # Email regex pattern (simplified RFC 5322 compliant)
    EMAIL_PATTERN = re.compile(
        r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$'
    )

    # Maximum length for email addresses (RFC 5321)
    MAX_EMAIL_LENGTH = 254
    MAX_LOCAL_LENGTH = 64
    MAX_DOMAIN_LENGTH = 255

    def __init__(self, strict_mode: bool = False):
        """
        Initialize the EmailValidator.

        Args:
            strict_mode: If True, applies stricter validation rules
        """
        self.strict_mode = strict_mode
        self.errors: List[str] = []

    def validate(self, email: str) -> bool:
        """
        Validate an email address.

        Args:
            email: The email address to validate

        Returns:
            True if the email is valid, False otherwise
        """
        self.errors.clear()

        if not email:
            self.errors.append("Email address cannot be empty")
            return False

        # Remove leading/trailing whitespace
        email = email.strip()

        # Check overall length
        if len(email) > self.MAX_EMAIL_LENGTH:
            self.errors.append(f"Email exceeds maximum length of {self.MAX_EMAIL_LENGTH} characters")
            return False

        # Check for @ symbol
        if email.count('@') != 1:
            self.errors.append("Email must contain exactly one @ symbol")
            return False

        # Split into local and domain parts
        local, domain = email.rsplit('@', 1)

        # Validate local part
        if not self._validate_local_part(local):
            return False

        # Validate domain part
        if not self._validate_domain_part(domain):
            return False

        # Apply regex pattern
        if not self.EMAIL_PATTERN.match(email):
            self.errors.append("Email format is invalid")
            return False

        return True

    def _validate_local_part(self, local: str) -> bool:
        """
        Validate the local part (before @) of the email.

        Args:
            local: The local part of the email

        Returns:
            True if valid, False otherwise
        """
        if not local:
            self.errors.append("Local part (before @) cannot be empty")
            return False

        if len(local) > self.MAX_LOCAL_LENGTH:
            self.errors.append(f"Local part exceeds maximum length of {self.MAX_LOCAL_LENGTH} characters")
            return False

        # Check for invalid starting/ending characters
        if local[0] == '.' or local[-1] == '.':
            self.errors.append("Local part cannot start or end with a period")
            return False

        # Check for consecutive periods
        if '..' in local:
            self.errors.append("Local part cannot contain consecutive periods")
            return False

        # In strict mode, apply additional checks
        if self.strict_mode:
            # Check for special characters
            allowed_special = set('._%+-')
            for char in local:
                if not (char.isalnum() or char in allowed_special):
                    self.errors.append(f"Local part contains invalid character: '{char}'")
                    return False

        return True

    def _validate_domain_part(self, domain: str) -> bool:
        """
        Validate the domain part (after @) of the email.

        Args:
            domain: The domain part of the email

        Returns:
            True if valid, False otherwise
        """
        if not domain:
            self.errors.append("Domain part (after @) cannot be empty")
            return False

        if len(domain) > self.MAX_DOMAIN_LENGTH:
            self.errors.append(f"Domain exceeds maximum length of {self.MAX_DOMAIN_LENGTH} characters")
            return False

        # Check for at least one period in domain
        if '.' not in domain:
            self.errors.append("Domain must contain at least one period")
            return False

        # Check for invalid starting/ending characters
        if domain[0] == '.' or domain[-1] == '.' or domain[0] == '-' or domain[-1] == '-':
            self.errors.append("Domain cannot start or end with a period or hyphen")
            return False

        # Check for consecutive periods
        if '..' in domain:
            self.errors.append("Domain cannot contain consecutive periods")
            return False

        # Validate domain labels
        labels = domain.split('.')

        # Check TLD length
        tld = labels[-1]
        if len(tld) < 2:
            self.errors.append("Top-level domain must be at least 2 characters")
            return False

        # Validate each label
        for label in labels:
            if not label:
                self.errors.append("Domain labels cannot be empty")
                return False

            if len(label) > 63:
                self.errors.append("Domain label exceeds maximum length of 63 characters")
                return False

            # Check label format
            if not re.match(r'^[a-zA-Z0-9-]+$', label):
                self.errors.append(f"Domain label '{label}' contains invalid characters")
                return False

        return True

    def is_valid(self, email: str) -> bool:
        """
        Convenience method to check if an email is valid.
        Alias for validate().

        Args:
            email: The email address to validate

        Returns:
            True if the email is valid, False otherwise
        """
        return self.validate(email)

    def get_errors(self) -> List[str]:
        """
        Get the list of validation errors from the last validation.

        Returns:
            List of error messages
        """
        return self.errors.copy()

    def validate_with_details(self, email: str) -> Tuple[bool, List[str]]:
        """
        Validate an email and return both the result and any errors.

        Args:
            email: The email address to validate

        Returns:
            A tuple of (is_valid, error_list)
        """
        is_valid = self.validate(email)
        return is_valid, self.get_errors()


# Example usage
if __name__ == "__main__":
    # Create validator instance
    validator = EmailValidator()

    # Test cases
    test_emails = [
        "user@example.com",
        "test.email+tag@domain.co.uk",
        "invalid.email@",
        "@invalid.com",
        "no-at-sign.com",
        "double@@example.com",
        "spaces in@example.com",
        ".starts-with-dot@example.com",
        "ends-with-dot.@example.com",
        "consecutive..dots@example.com",
        "user@domain",
        "user@.invalid.com",
        "valid_email123@test-domain.org",
    ]

    print("Email Validation Results:")
    print("=" * 60)

    for email in test_emails:
        is_valid, errors = validator.validate_with_details(email)
        status = "✓ VALID" if is_valid else "✗ INVALID"
        print(f"\n{status}: {email}")
        if errors:
            for error in errors:
                print(f"  - {error}")
