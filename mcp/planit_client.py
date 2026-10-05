# planit_client.py
import asyncio
import logging
import time

import httpx

from config import API_BASE_URL, SERVICE_ACCOUNT_EMAIL, SERVICE_ACCOUNT_PASSWORD

logger = logging.getLogger(__name__)

# Refresh slightly before actual expiry so an in-flight request never gets a token that
# expires mid-call.
_EXPIRY_SAFETY_BUFFER_SECONDS = 30


class _TokenCache:
    def __init__(self) -> None:
        self.access_token: str | None = None
        self.refresh_token: str | None = None
        self.expires_at: float = 0.0
        self.lock = asyncio.Lock()


_cache = _TokenCache()


def _store_auth_response(data: dict) -> None:
    _cache.access_token = data["accessToken"]
    _cache.refresh_token = data["refreshToken"]
    _cache.expires_at = time.monotonic() + data["expiresInSeconds"] - _EXPIRY_SAFETY_BUFFER_SECONDS


async def _login(client: httpx.AsyncClient) -> None:
    if not SERVICE_ACCOUNT_EMAIL or not SERVICE_ACCOUNT_PASSWORD:
        raise RuntimeError(
            "PLANIT_SERVICE_ACCOUNT_EMAIL / PLANIT_SERVICE_ACCOUNT_PASSWORD are not set"
        )

    response = await client.post(
        f"{API_BASE_URL}/auth/login",
        json={"usernameOrEmail": SERVICE_ACCOUNT_EMAIL, "password": SERVICE_ACCOUNT_PASSWORD},
    )
    response.raise_for_status()
    _store_auth_response(response.json())
    logger.info("Logged in to PlanIt API as service account")


async def _refresh(client: httpx.AsyncClient) -> None:
    response = await client.post(
        f"{API_BASE_URL}/auth/refresh",
        json={"refreshToken": _cache.refresh_token},
    )
    response.raise_for_status()
    _store_auth_response(response.json())
    logger.info("Refreshed PlanIt API access token")


async def get_access_token() -> str:
    """Return a valid access token, logging in or refreshing as needed.

    Safe to call concurrently — only one login/refresh happens at a time.
    """
    async with _cache.lock:
        if _cache.access_token and time.monotonic() < _cache.expires_at:
            return _cache.access_token

        async with httpx.AsyncClient() as client:
            if _cache.refresh_token:
                try:
                    await _refresh(client)
                    return _cache.access_token
                except httpx.HTTPStatusError:
                    logger.warning("Refresh token rejected, falling back to login")

            await _login(client)
            return _cache.access_token


async def authorized_request(method: str, path: str, **kwargs) -> httpx.Response:
    """Make an authenticated request against the PlanIt API, retrying once on 401
    in case the access token was revoked server-side between calls."""
    for attempt in range(2):
        token = await get_access_token()
        async with httpx.AsyncClient() as client:
            response = await client.request(
                method,
                f"{API_BASE_URL}{path}",
                headers={"Authorization": f"Bearer {token}"},
                **kwargs,
            )
        if response.status_code == 401 and attempt == 0:
            _cache.access_token = None
            continue
        response.raise_for_status()
        return response

    response.raise_for_status()
    return response
