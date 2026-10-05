# config.py
import os

ENVIRONMENT = os.getenv("ENVIRONMENT", "development")

# .env is for local development only. In production these come from real environment
# variables (container/host config), and python-dotenv isn't even installed there —
# so this import must stay inside the conditional.
if ENVIRONMENT != "production":
    from dotenv import load_dotenv
    load_dotenv()

API_BASE_URL = os.getenv("PLANIT_API_URL", "http://localhost:5223")
SERVICE_ACCOUNT_EMAIL = os.getenv("PLANIT_SERVICE_ACCOUNT_EMAIL", "")
SERVICE_ACCOUNT_PASSWORD = os.getenv("PLANIT_SERVICE_ACCOUNT_PASSWORD", "")
