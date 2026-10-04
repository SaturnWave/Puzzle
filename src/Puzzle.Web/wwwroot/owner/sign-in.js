// The owner's key form on a public server: sends the key, and opens the lab once the server has set the owner cookie.

const form = document.getElementById("key-form");
const input = document.getElementById("key-input");
const button = document.getElementById("key-submit");
const error = document.getElementById("key-error");

/** What the owner reads when the server refuses; a status it does not name is shown as it came. */
function refusal(status) {
  if (status === 401) return "Anahtar yanlış.";
  if (status === 429) return "Çok fazla deneme. Bir dakika sonra tekrar dene.";
  return `Giriş yapılamadı (${status}).`;
}

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  error.hidden = true;
  button.disabled = true;
  button.classList.add("is-busy");
  try {
    // The header marks the request as this page's own; the server refuses changes that come without it.
    const response = await fetch("/api/owner/sign-in", {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Puzzle-Client": "owner" },
      body: JSON.stringify({ key: input.value }),
    });
    if (response.ok) {
      location.assign("/lab/");
      return;
    }
    error.textContent = refusal(response.status);
  } catch (failure) {
    if (!(failure instanceof TypeError)) throw failure;
    error.textContent = "Sunucuya ulaşılamadı. Bağlantını kontrol edip tekrar dene.";
  }
  error.hidden = false;
  button.disabled = false;
  button.classList.remove("is-busy");
  input.select();
});
