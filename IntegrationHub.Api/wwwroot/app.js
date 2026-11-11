'use strict';

const API_BASE = '/api';
const TOKEN_KEY = 'ih_token';
const USER_KEY = 'ih_user';
const MIN_CALCULATING_DISPLAY_MS = 400;

const el = (id) => document.getElementById(id);
const token = () => localStorage.getItem(TOKEN_KEY);
const isLoggedIn = () => !!token();

let categories = [];
let activeCategory = null;
let activeSub = null;
let authSessionVersion = 0;
let historyRequestVersion = 0;
let passwordUserId = null;
let sharedDataEditId = null;
let detectedLocation = null;
let locationRequestInProgress = false;
let locationRequestId = 0;

// ---------- HTTP helpers ----------
async function api(path, { method = 'GET', body } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  const t = token();
  if (t) headers['Authorization'] = `Bearer ${t}`;
  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
    cache: 'no-store',
    body: body ? JSON.stringify(body) : undefined
  });
  const text = await res.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  if (!res.ok) {
    const msg = (data && data.error) ? data.error : `İstek başarısız (${res.status})`;
    throw new Error(msg);
  }
  return data;
}

function toast(message, kind = 'info') {
  const t = el('toast');
  const colors = {
    info: 'bg-slate-800 text-white',
    success: 'bg-emerald-600 text-white',
    error: 'bg-red-600 text-white'
  };
  t.className = `fixed bottom-6 left-1/2 -translate-x-1/2 z-50 px-4 py-2.5 rounded-xl text-sm shadow-lg ${colors[kind] || colors.info}`;
  t.textContent = message;
  t.classList.remove('hidden');
  setTimeout(() => t.classList.add('hidden'), 2800);
}

function playEntrance(element) {
  element.classList.remove('screen-enter');
  void element.offsetWidth;
  element.classList.add('screen-enter');
}

// ---------- Auth ----------
let mode = 'login';
function setMode(m) {
  mode = m;
  const isLogin = m === 'login';
  el('tabLogin').classList.toggle('bg-white', isLogin);
  el('tabLogin').classList.toggle('shadow-sm', isLogin);
  el('tabLogin').classList.toggle('text-brand-700', isLogin);
  el('tabLogin').classList.toggle('text-slate-500', !isLogin);
  el('tabRegister').classList.toggle('bg-white', !isLogin);
  el('tabRegister').classList.toggle('shadow-sm', !isLogin);
  el('tabRegister').classList.toggle('text-brand-700', !isLogin);
  el('tabRegister').classList.toggle('text-slate-500', isLogin);
  el('usernameField').classList.toggle('hidden', isLogin);
  el('username').required = !isLogin;
  el('confirmPasswordField').classList.toggle('hidden', isLogin);
  el('confirmPassword').required = !isLogin;
  el('authSubmit').textContent = isLogin ? 'Giriş Yap' : 'Kayıt Ol';
  el('password').autocomplete = isLogin ? 'current-password' : 'new-password';
  el('authError').classList.add('hidden');
}

function validEmail(value) {
  return /^[^\s@]+@[^\s@]+\.[A-Za-z]{2,}$/.test(value);
}

el('tabLogin').onclick = () => setMode('login');
el('tabRegister').onclick = () => setMode('register');

el('authForm').addEventListener('submit', async (e) => {
  e.preventDefault();
  el('authError').classList.add('hidden');
  const submitMode = mode;
  const email = el('email').value.trim();
  const password = el('password').value;
  const username = el('username').value.trim();
  if (!validEmail(email)) {
    el('authError').textContent = 'Lütfen geçerli bir e-posta adresi girin (ör. ad@ornek.com).';
    el('authError').classList.remove('hidden');
    return;
  }
  if (!password || (submitMode === 'register' && (password.length < 6 || username.length < 3))) {
    el('authError').textContent = submitMode === 'register'
      ? 'Kullanıcı adı en az 3, şifre en az 6 karakter olmalıdır.'
      : 'Lütfen e-posta ve şifre alanlarını doldurun.';
    el('authError').classList.remove('hidden');
    return;
  }
  if (submitMode === 'register' && password !== el('confirmPassword').value) {
    el('authError').textContent = 'Şifreler birbiriyle eşleşmiyor.';
    el('authError').classList.remove('hidden');
    return;
  }
  el('authSubmit').disabled = true;
  el('tabLogin').disabled = true;
  el('tabRegister').disabled = true;
  el('authSubmit').textContent = submitMode === 'login' ? 'Giriş yapılıyor...' : 'Kayıt yapılıyor...';
  try {
    const payload = {
      email,
      password
    };
    if (submitMode === 'register') payload.username = username;

    const data = await api(`/auth/${submitMode}`, { method: 'POST', body: payload });
    localStorage.setItem(TOKEN_KEY, data.token);
    localStorage.setItem(USER_KEY, JSON.stringify({
      id: data.userId,
      username: data.username,
      email: data.email,
      isAdmin: data.isAdmin
    }));
    authSessionVersion++;
    enterApp(authSessionVersion);
  } catch (err) {
    el('authError').textContent = err.message;
    el('authError').classList.remove('hidden');
  } finally {
    el('authSubmit').disabled = false;
    el('tabLogin').disabled = false;
    el('tabRegister').disabled = false;
    el('authSubmit').textContent = mode === 'login' ? 'Giriş Yap' : 'Kayıt Ol';
  }
});

function clearSessionUi() {
  locationRequestId++;
  locationRequestInProgress = false;
  detectedLocation = null;
  activeCategory = null;
  activeSub = null;
  categories = [];
  passwordUserId = null;
  sharedDataEditId = null;
  el('categoryGrid').innerHTML = '';
  el('inputPanel').innerHTML = '';
  el('inputPanel').classList.add('hidden');
  el('appScreen').classList.add('hidden');
  el('topBar').classList.add('hidden');
  el('adminBtn').classList.add('hidden');
  el('adminModal').classList.add('hidden');
  el('adminHistoryModal').classList.add('hidden');
  el('passwordModal').classList.add('hidden');
  el('historyModal').classList.add('hidden');
  el('sharedDataModal').classList.add('hidden');
  el('historyList').innerHTML = '';
  el('clearHistoryBtn').disabled = false;
  el('clearHistoryBtn').textContent = 'Geçmişi temizle';
  historyRequestVersion++;
  el('sharedDataList').innerHTML = '';
  el('sharedDataType').value = '';
  el('sharedDataFrom').value = '';
  el('sharedDataTo').value = '';
  el('adminUsers').innerHTML = '';
  el('adminSharedList').innerHTML = '';
  el('adminHistoryList').innerHTML = '';
  el('adminHistoryTitle').textContent = 'Kullanıcı geçmişi';
  el('adminError').textContent = '';
  el('adminError').classList.add('hidden');
  el('createUserForm').reset();
  el('passwordForm').reset();
  el('adminSharedForm').reset();
  el('adminSharedPayload').value = '';
  el('adminSharedSave').textContent = 'Kaydı ekle';
  el('adminSharedCancel').classList.add('hidden');
  el('userBadge').querySelector('.user-badge-text').textContent = '';
  el('toast').classList.add('hidden');
  el('toast').textContent = '';
}

el('logoutBtn').onclick = () => {
  authSessionVersion++;
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
  clearSessionUi();
  el('authScreen').classList.remove('hidden');
  playEntrance(el('authScreen').firstElementChild);
  setMode('login');
  el('authForm').reset();
};

// ---------- App bootstrap ----------
async function enterApp(sessionVersion = authSessionVersion) {
  try {
    const currentUser = await api('/auth/me');
    if (sessionVersion !== authSessionVersion) return;
    const u = {
      id: currentUser.userId,
      username: currentUser.username,
      email: currentUser.email,
      isAdmin: currentUser.isAdmin
    };
    localStorage.setItem(USER_KEY, JSON.stringify(u));
    el('userBadge').querySelector('.user-badge-text').textContent = u.username ? `Merhaba, ${u.username}` : '';
    el('adminBtn').classList.toggle('hidden', !u.isAdmin);
    el('authScreen').classList.add('hidden');
    el('appScreen').classList.remove('hidden');
    playEntrance(el('appScreen').firstElementChild);
    el('topBar').classList.remove('hidden');
    const meta = await api('/search/init');
    if (sessionVersion !== authSessionVersion) return;
    categories = meta.categories || [];
    renderCategories();
  } catch (err) {
    const message = err.message.toLowerCase();
    if (message.includes('token') || message.includes('kimlik') || message.includes('hesap')) {
      el('logoutBtn').click();
    } else {
      toast('Kategoriler yüklenemedi: ' + err.message, 'error');
    }
  }
}

// ---------- Admin panel ----------
    el('adminBtn').onclick = async () => {
      el('adminModal').classList.remove('hidden');
      setAdminSection('users');
      await loadAdminUsers();
    };
    el('adminUsersTab').onclick = () => setAdminSection('users');
    el('adminDataTab').onclick = async () => {
      setAdminSection('shared');
      if (!el('adminSharedDate').value) {
        el('adminSharedDate').value = getSharedDataDefaultDate(el('adminSharedType').value);
      }
      await loadAdminSharedData();
    };
    el('closeAdmin').onclick = () => el('adminModal').classList.add('hidden');
    el('adminModal').addEventListener('click', (event) => {
      if (event.target === el('adminModal')) el('adminModal').classList.add('hidden');
    });
    el('closeAdminHistory').onclick = () => el('adminHistoryModal').classList.add('hidden');
    el('adminHistoryModal').addEventListener('click', (event) => {
      if (event.target === el('adminHistoryModal')) el('adminHistoryModal').classList.add('hidden');
    });
    el('cancelPassword').onclick = () => el('passwordModal').classList.add('hidden');
    el('adminSharedFilter').onclick = loadAdminSharedData;
    el('adminSharedCancel').onclick = resetAdminSharedForm;
    el('adminSharedType').addEventListener('change', () => {
      if (!sharedDataEditId) {
        el('adminSharedDate').value = getSharedDataDefaultDate(el('adminSharedType').value);
      }
    });
    el('adminSharedForm').addEventListener('submit', async (event) => {
      event.preventDefault();
      clearAdminError();
      let payload;
      try {
        payload = JSON.parse(el('adminSharedPayload').value);
      } catch {
        showAdminError('Veri içeriği geçerli JSON olmalıdır.');
        return;
      }

      const request = {
        dataType: el('adminSharedType').value,
        recordKey: el('adminSharedKey').value.trim(),
        displayName: el('adminSharedName').value.trim(),
        dataDate: el('adminSharedDate').value,
        payloadJson: JSON.stringify(payload)
      };
      try {
        await api(sharedDataEditId ? `/admin/shared-data/${sharedDataEditId}` : '/admin/shared-data', {
          method: sharedDataEditId ? 'PUT' : 'POST',
          body: request
        });
        toast(sharedDataEditId ? 'Ortak veri güncellendi.' : 'Ortak veri kaydedildi.', 'success');
        resetAdminSharedForm();
        await loadAdminSharedData();
      } catch (error) {
        showAdminError(error.message);
      }
    });
    el('adminSharedList').addEventListener('click', async (event) => {
      const button = event.target.closest('button[data-shared-action]');
      if (!button) return;
      const id = Number(button.closest('[data-shared-id]')?.dataset.sharedId);
      if (!id) return;

      clearAdminError();
      try {
        if (button.dataset.sharedAction === 'edit') {
          const record = await api(`/admin/shared-data/${id}`);
          sharedDataEditId = record.id;
          el('adminSharedType').value = record.dataType;
          el('adminSharedKey').value = record.recordKey;
          el('adminSharedName').value = record.displayName;
          el('adminSharedDate').value = record.dataDate;
          el('adminSharedPayload').value = JSON.stringify(JSON.parse(record.payloadJson), null, 2);
          el('adminSharedSave').textContent = 'Değişiklikleri kaydet';
          el('adminSharedCancel').classList.remove('hidden');
          el('adminSharedForm').scrollIntoView({ behavior: 'smooth', block: 'start' });
        } else if (button.dataset.sharedAction === 'delete') {
          if (!window.confirm('Bu ortak veri kaydı kalıcı olarak silinsin mi?')) return;
          await api(`/admin/shared-data/${id}`, { method: 'DELETE' });
          toast('Ortak veri silindi.', 'success');
          if (sharedDataEditId === id) resetAdminSharedForm();
          await loadAdminSharedData();
        }
      } catch (error) {
        showAdminError(error.message);
      }
    });
    el('createUserForm').addEventListener('submit', async (event) => {
      event.preventDefault();
      clearAdminError();
      const email = el('newUserEmail').value.trim();
      if (!validEmail(email)) {
        showAdminError('Lütfen geçerli bir e-posta adresi girin (ör. ad@ornek.com).');
        return;
      }
      if (el('newUsername').value.trim().length < 3 || el('newUserPassword').value.length < 6) {
        showAdminError('Kullanıcı adı en az 3, şifre en az 6 karakter olmalıdır.');
        return;
      }
      try {
        await api('/admin/users', {
          method: 'POST',
          body: {
            email,
            username: el('newUsername').value.trim(),
            password: el('newUserPassword').value
          }
        });
        el('createUserForm').reset();
        toast('Kullanıcı oluşturuldu.', 'success');
        await loadAdminUsers();
      } catch (error) {
        showAdminError(error.message);
      }
    });

    el('adminUsers').addEventListener('click', async (event) => {
      const button = event.target.closest('button[data-action]');
      if (!button) return;
      const row = button.closest('[data-user-id]');
      if (!row) return;
      const userId = Number(row.dataset.userId);
      const action = button.dataset.action;
      clearAdminError();

      try {
        if (action === 'save') {
          const email = row.querySelector('[data-field="email"]').value.trim();
          if (!validEmail(email)) {
            showAdminError('Lütfen kullanıcı için geçerli bir e-posta adresi girin.');
            return;
          }
          await api(`/admin/users/${userId}`, {
            method: 'PUT',
            body: {
              email,
              username: row.querySelector('[data-field="username"]').value.trim(),
              isActive: row.querySelector('[data-field="active"]').checked,
              isAdmin: row.querySelector('[data-field="admin"]').checked
            }
          });
          toast('Kullanıcı bilgileri güncellendi.', 'success');
          await loadAdminUsers();
        } else if (action === 'password') {
          passwordUserId = userId;
          el('passwordModalTitle').textContent = `Şifre belirle: ${row.querySelector('[data-field="username"]').value}`;
          el('passwordError').classList.add('hidden');
          el('passwordForm').reset();
          el('passwordModal').classList.remove('hidden');
        } else if (action === 'history') {
          const items = await api(`/admin/users/${userId}/history`);
          el('adminHistoryTitle').textContent = `${row.querySelector('[data-field="username"]').value} - işlem geçmişi`;
          renderHistoryInto(el('adminHistoryList'), items, 'Bu kullanıcının işlem geçmişi yok.');
          el('adminHistoryModal').classList.remove('hidden');
        } else if (action === 'clear-history') {
          if (!window.confirm('Bu kullanıcının işlem geçmişi silinsin mi? Hesap silinmeyecek.')) return;
          await api(`/admin/users/${userId}/history`, { method: 'DELETE' });
          toast('İşlem geçmişi silindi.', 'success');
        } else if (action === 'delete') {
          if (!window.confirm('Kullanıcı ve işlem geçmişi kalıcı olarak silinsin mi?')) return;
          await api(`/admin/users/${userId}`, { method: 'DELETE' });
          toast('Kullanıcı silindi.', 'success');
          await loadAdminUsers();
        }
      } catch (error) {
        showAdminError(error.message);
      }
    });

    el('passwordForm').addEventListener('submit', async (event) => {
      event.preventDefault();
      const password = el('adminPassword').value;
      const error = el('passwordError');
      error.classList.add('hidden');
      if (password.length < 6) {
        error.textContent = 'Şifre en az 6 karakter olmalıdır.';
        error.classList.remove('hidden');
        return;
      }
      if (password !== el('adminPasswordConfirm').value) {
        error.textContent = 'Şifreler birbiriyle eşleşmiyor.';
        error.classList.remove('hidden');
        return;
      }
      try {
        await api(`/admin/users/${passwordUserId}/password`, {
          method: 'POST',
          body: { password }
        });
        el('passwordModal').classList.add('hidden');
        toast('Şifre güncellendi.', 'success');
      } catch (err) {
        error.textContent = err.message;
        error.classList.remove('hidden');
      }
    });

async function loadAdminUsers() {
      const list = el('adminUsers');
      list.innerHTML = '<div class="text-center text-slate-400 py-6">Yükleniyor...</div>';
      clearAdminError();
      try {
        const users = await api('/admin/users');
        if (!users.length) {
          list.innerHTML = '<div class="text-center text-slate-400 py-6">Kullanıcı bulunamadı.</div>';
          return;
        }
        list.innerHTML = '';
        users.forEach((user) => {
          const row = document.createElement('div');
          row.dataset.userId = user.id;
          row.className = 'border border-slate-200 rounded-xl p-3 space-y-3';
          row.innerHTML = `
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
              <label class="text-xs text-slate-500">Kullanıcı adı<input data-field="username" value="${escapeHtml(user.username)}" class="mt-1 w-full px-2.5 py-2 text-sm rounded-lg border border-slate-200"></label>
              <label class="text-xs text-slate-500">E-posta<input data-field="email" type="email" value="${escapeHtml(user.email)}" class="mt-1 w-full px-2.5 py-2 text-sm rounded-lg border border-slate-200"></label>
            </div>
            <div class="flex flex-wrap items-center gap-3 text-sm">
              <label class="flex items-center gap-1"><input data-field="active" type="checkbox" ${user.isActive ? 'checked' : ''}> Etkin</label>
              <label class="flex items-center gap-1"><input data-field="admin" type="checkbox" ${user.isAdmin ? 'checked' : ''}> Yönetici</label>
              <span class="text-xs text-slate-400">Kayıt: ${formatDate(user.createdAt)}</span>
            </div>
            <div class="flex flex-wrap gap-2">
              <button data-action="save" class="px-3 py-1.5 rounded-lg bg-brand-600 text-white text-xs">Kaydet</button>
              <button data-action="password" class="px-3 py-1.5 rounded-lg bg-slate-100 text-slate-700 text-xs">Şifre belirle</button>
              <button data-action="history" class="px-3 py-1.5 rounded-lg bg-slate-100 text-slate-700 text-xs">Geçmiş</button>
              <button data-action="clear-history" class="px-3 py-1.5 rounded-lg bg-amber-50 text-amber-700 text-xs">Geçmişi sil</button>
              <button data-action="delete" class="px-3 py-1.5 rounded-lg bg-red-50 text-red-600 text-xs">Kullanıcıyı sil</button>
            </div>`;
          list.appendChild(row);
        });
      } catch (error) {
        list.innerHTML = '';
        showAdminError(error.message);
      }
    }

function setAdminSection(section) {
  const isShared = section === 'shared';
  el('adminUsersSection').classList.toggle('hidden', isShared);
  el('adminSharedSection').classList.toggle('hidden', !isShared);
  el('adminUsersTab').className = `px-3 py-2 rounded-lg text-sm ${isShared ? 'bg-slate-100 text-slate-700' : 'bg-brand-600 text-white'}`;
  el('adminDataTab').className = `px-3 py-2 rounded-lg text-sm ${isShared ? 'bg-brand-600 text-white' : 'bg-slate-100 text-slate-700'}`;
  clearAdminError();
}

function resetAdminSharedForm() {
  sharedDataEditId = null;
  el('adminSharedForm').reset();
  el('adminSharedDate').value = getSharedDataDefaultDate(el('adminSharedType').value);
  el('adminSharedPayload').value = '';
  el('adminSharedSave').textContent = 'Kaydı ekle';
  el('adminSharedCancel').classList.add('hidden');
}

function getSharedDataDefaultDate(type) {
  const offsetMs = type === 'prayer' ? 3 * 60 * 60 * 1000 : 0;
  return new Date(Date.now() + offsetMs).toISOString().slice(0, 10);
}

async function loadAdminSharedData() {
  const list = el('adminSharedList');
  list.innerHTML = '<div class="text-center text-slate-400 py-6">Yükleniyor...</div>';
  clearAdminError();
  const query = buildSharedDataQuery('adminSharedFilterType', 'adminSharedFrom', 'adminSharedTo');
  try {
    const records = await api(`/admin/shared-data?${query}`);
    renderSharedDataInto(list, records, 'Bu filtrelerde ortak veri bulunamadı.', true);
  } catch (error) {
    list.innerHTML = '';
    showAdminError(error.message);
  }
}

function showAdminError(message) {
  el('adminError').textContent = message;
  el('adminError').classList.remove('hidden');
}

function clearAdminError() {
  el('adminError').textContent = '';
  el('adminError').classList.add('hidden');
}

// ---------- Render category cards ----------
const ICONS = {
  currency: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-6 h-6"><circle cx="12" cy="12" r="9"/><path d="M14.5 9.5c-.5-.8-1.5-1.3-2.5-1.3-1.4 0-2.5.8-2.5 1.8s1 1.5 2.5 1.8 2.5.9 2.5 1.9-1.1 1.8-2.5 1.8c-1 0-2-.5-2.5-1.3M12 7v1.2M12 14.8V16"/></svg>',
  gold: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-6 h-6"><circle cx="12" cy="12" r="9"/><path d="M9 15V9h3a2 2 0 010 4H9m0 0h3"/></svg>',
  weather: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-6 h-6"><path d="M17 18a4 4 0 100-8 5 5 0 00-9.8 1.5A3.5 3.5 0 007 18h10z"/></svg>',
  prayer: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-6 h-6"><circle cx="12" cy="12" r="9"/><path stroke-linecap="round" stroke-linejoin="round" d="M12 7v5l3 2"/></svg>',
  calculator: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-6 h-6"><rect x="5" y="3" width="14" height="18" rx="2"/><path d="M9 7h6M9 11h2M13 11h2M9 15h2M13 15h2"/></svg>'
};

function renderCategories() {
  const grid = el('categoryGrid');
  grid.innerHTML = '';
  categories.forEach((cat) => {
    const icon = ICONS[cat.icon] || ICONS.calculator;
    const card = document.createElement('button');
    card.className = 'category-card group bg-white rounded-2xl shadow-sm border border-slate-200/70 hover:border-brand-400 hover:shadow-md transition p-5 text-left flex flex-col gap-3';
    card.innerHTML = `
      <div class="category-card-icon w-11 h-11 rounded-xl bg-brand-50 text-brand-600 grid place-items-center group-hover:bg-brand-600 group-hover:text-white transition">${icon}</div>
      <div class="category-card-copy">
        <div class="font-semibold text-slate-800">${escapeHtml(cat.displayName)}</div>
        <div class="text-xs text-slate-400 mt-0.5">${cat.serviceType === 'currency'
          ? 'Kur hesaplama'
          : cat.serviceType === 'gold'
            ? 'Altın hesaplama'
            : cat.subCategories.length === 1
              ? escapeHtml(cat.subCategories[0].displayName)
              : `${cat.subCategories.length} alt kategori`}</div>
      </div>
      <span class="category-card-arrow" aria-hidden="true">›</span>`;
    card.onclick = () => selectCategory(cat);
    grid.appendChild(card);
  });
}

function selectCategory(cat) {
  activeCategory = cat;
  activeSub = cat.subCategories.length === 1 || cat.serviceType === 'currency' || cat.serviceType === 'gold'
    ? cat.subCategories[0]
    : null;
  document.querySelectorAll('#categoryGrid button').forEach((b, i) => {
    b.classList.toggle('ring-2', categories[i] === cat);
    b.classList.toggle('ring-brand-400', categories[i] === cat);
  });
  renderSubCategories();
  playEntrance(el('inputPanel'));
}

// ---------- Dynamic input panel ----------
function renderSubCategories() {
  const panel = el('inputPanel');
  panel.classList.remove('hidden');
  panel.innerHTML = '';

  const title = document.createElement('div');
  title.className = 'flex items-center justify-between mb-1';
  title.innerHTML = `<h3 class="font-semibold text-slate-800">${escapeHtml(activeCategory.displayName)}</h3>
    <button id="closePanel" type="button" aria-label="Paneli kapat" title="Paneli kapat"
      class="grid h-9 w-9 place-items-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-700 text-xl leading-none">&times;</button>`;
  panel.appendChild(title);

  if (['currency', 'gold'].includes(activeCategory.serviceType)) {
    const optionField = document.createElement('div');
    optionField.className = 'relative z-20';
    const label = document.createElement('label');
    label.className = 'block text-sm font-medium text-slate-600 mb-1';
    label.textContent = activeCategory.serviceType === 'gold' ? 'Altın türü' : 'Döviz türü';
    optionField.appendChild(label);

    const trigger = document.createElement('button');
    trigger.type = 'button';
    trigger.className = 'flex w-full items-center justify-between rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-left text-sm text-slate-700 shadow-sm transition hover:border-brand-300 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-100';
    trigger.setAttribute('aria-haspopup', 'listbox');
    trigger.setAttribute('aria-expanded', 'false');

    const selectedLabel = document.createElement('span');
    selectedLabel.className = 'font-medium';
    selectedLabel.textContent = activeCategory.serviceType === 'currency'
      ? `${activeSub.displayName} (${activeSub.key})`
      : activeSub.displayName;
    const chevron = document.createElement('span');
    chevron.className = 'ml-3 text-slate-400';
    chevron.setAttribute('aria-hidden', 'true');
    chevron.textContent = '▾';
    trigger.append(selectedLabel, chevron);
    optionField.appendChild(trigger);

    const options = document.createElement('div');
    options.id = `${activeCategory.serviceType}Options`;
    options.className = 'absolute left-0 right-0 top-full z-50 mt-2 hidden max-h-60 overflow-y-auto rounded-xl border border-slate-200 bg-white p-1.5 shadow-xl';
    options.setAttribute('role', 'listbox');
    options.setAttribute('aria-label', label.textContent);

    const optionButtons = [];
    activeCategory.subCategories.forEach((sub) => {
      const option = document.createElement('button');
      option.type = 'button';
      option.className = 'flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-sm transition hover:bg-brand-50 hover:text-brand-700';
      option.setAttribute('role', 'option');
      option.setAttribute('aria-selected', String(sub.key === activeSub.key));
      option.textContent = sub.displayName;
      const code = document.createElement('span');
      code.className = 'ml-3 text-xs text-slate-400';
      code.textContent = activeCategory.serviceType === 'currency' ? sub.key : '';
      option.appendChild(code);
      option.addEventListener('click', () => {
        activeSub = sub;
        selectedLabel.textContent = activeCategory.serviceType === 'currency'
          ? `${sub.displayName} (${sub.key})`
          : sub.displayName;
        optionButtons.forEach((button) => {
          const selected = button.dataset.optionKey === sub.key;
          button.setAttribute('aria-selected', String(selected));
          button.classList.toggle('bg-brand-50', selected);
          button.classList.toggle('text-brand-700', selected);
        });
        closeOptions();
        buildDynamicForm(activeSub);
      });
      option.dataset.optionKey = sub.key;
      optionButtons.push(option);
      options.appendChild(option);
    });

    let outsidePointerHandler = null;
    const closeOptions = () => {
      options.classList.add('hidden');
      trigger.setAttribute('aria-expanded', 'false');
      if (outsidePointerHandler) {
        document.removeEventListener('pointerdown', outsidePointerHandler, true);
        outsidePointerHandler = null;
      }
    };
    const openOptions = () => {
      if (!options.classList.contains('hidden')) return;
      options.classList.remove('hidden');
      trigger.setAttribute('aria-expanded', 'true');
      outsidePointerHandler = (event) => {
        if (!optionField.contains(event.target)) closeOptions();
      };
      document.addEventListener('pointerdown', outsidePointerHandler, true);
    };

    trigger.setAttribute('aria-controls', options.id);
    trigger.addEventListener('click', () => {
      if (options.classList.contains('hidden')) openOptions();
      else closeOptions();
    });
    trigger.addEventListener('keydown', (event) => {
      if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
      event.preventDefault();
      openOptions();
      const selectedIndex = activeCategory.subCategories.findIndex((sub) => sub.key === activeSub.key);
      const nextIndex = event.key === 'ArrowDown'
        ? Math.min(selectedIndex + 1, optionButtons.length - 1)
        : Math.max(selectedIndex - 1, 0);
      optionButtons[nextIndex]?.focus();
    });
    options.addEventListener('keydown', (event) => {
      const currentIndex = optionButtons.indexOf(event.target);
      let nextIndex = currentIndex;
      if (event.key === 'ArrowDown') nextIndex = Math.min(currentIndex + 1, optionButtons.length - 1);
      else if (event.key === 'ArrowUp') nextIndex = Math.max(currentIndex - 1, 0);
      else if (event.key === 'Home') nextIndex = 0;
      else if (event.key === 'End') nextIndex = optionButtons.length - 1;
      else if (event.key === 'Escape') {
        event.preventDefault();
        closeOptions();
        trigger.focus();
        return;
      } else {
        return;
      }
      event.preventDefault();
      optionButtons[nextIndex]?.focus();
    });
    optionField.appendChild(options);
    panel.appendChild(optionField);
  }

  if (activeCategory.subCategories.length > 1 &&
      !['currency', 'gold'].includes(activeCategory.serviceType)) {
    const chips = document.createElement('div');
    chips.className = 'flex flex-wrap gap-2 mb-5';
    activeCategory.subCategories.forEach((sub) => {
      const c = document.createElement('button');
      c.type = 'button';
      c.className = 'px-3.5 py-1.5 rounded-full text-sm border transition ' +
        (activeSub === sub
          ? 'bg-brand-600 text-white border-brand-600'
          : 'bg-slate-50 text-slate-600 border-slate-200 hover:border-brand-300');
      c.textContent = sub.displayName;
      c.onclick = () => { activeSub = sub; renderSubCategories(); };
      chips.appendChild(c);
    });
    panel.appendChild(chips);
  }

  if (!activeSub) {
    if (activeCategory.subCategories.length > 0) {
      const hint = document.createElement('p');
      hint.className = 'text-sm text-slate-500';
      hint.textContent = 'Devam etmek için bir alt kategori seçin.';
      panel.appendChild(hint);
    }
    el('closePanel').onclick = () => { panel.classList.add('hidden'); activeCategory = null; activeSub = null; renderCategories(); };
    return;
  }

  buildDynamicForm(activeSub);
  el('closePanel').onclick = () => { panel.classList.add('hidden'); activeCategory = null; activeSub = null; renderCategories(); };
}

function buildDynamicForm(sub) {
  const panel = el('inputPanel');
  const existing = panel.querySelector('#dynamicForm');
  if (existing) existing.remove();

  const form = document.createElement('form');
  form.id = 'dynamicForm';
  form.className = 'mt-2 space-y-4 animate-fade-in';
  form.noValidate = true;
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    void handleCalculate();
  });

  // Direction selector (bidirectional)
  if (sub.inputKind === 'bidirectional' && sub.options.length) {
    const dirWrap = document.createElement('div');
    dirWrap.className = 'flex flex-wrap gap-2';
    sub.options.forEach((opt, i) => {
      const b = document.createElement('button');
      b.type = 'button';
      b.dataset.direction = opt.value;
      b.className = 'px-3 py-1.5 rounded-lg text-sm border transition ' +
        (i === 0 ? 'bg-brand-50 border-brand-300 text-brand-700' : 'bg-slate-50 border-slate-200 text-slate-600 hover:border-brand-300');
      b.textContent = opt.label;
      b.onclick = () => {
        dirWrap.querySelectorAll('button').forEach((x) => {
          x.className = 'px-3 py-1.5 rounded-lg text-sm border transition bg-slate-50 border-slate-200 text-slate-600 hover:border-brand-300';
        });
        b.className = 'px-3 py-1.5 rounded-lg text-sm border transition bg-brand-50 border-brand-300 text-brand-700';
      };
      dirWrap.appendChild(b);
    });
    form.appendChild(dirWrap);
  }

  // Input field
  const inputType = sub.inputKind === 'text' ? 'text' : 'number';
  const field = document.createElement('div');
  const supportsLocation = ['weather', 'prayer'].includes(activeCategory.serviceType);
  field.innerHTML = `
    <div class="mb-1 flex items-center justify-between gap-2">
      <label for="dynInput" class="block text-sm font-medium text-slate-600">${escapeHtml(sub.inputLabel || 'Değer')}</label>
      ${supportsLocation ? `
        <button id="locationPermissionBtn" type="button"
          class="inline-flex min-h-8 shrink-0 items-center gap-1.5 rounded-lg px-2.5 text-xs font-semibold text-brand-700 transition hover:bg-brand-50 disabled:cursor-wait disabled:opacity-60">
          <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" class="h-4 w-4">
            <circle cx="12" cy="12" r="7.5"/><circle cx="12" cy="12" r="2.5"/>
            <path stroke-linecap="round" d="M12 2v3M12 19v3M2 12h3M19 12h3"/>
          </svg>
          <span class="location-button-label">Konumumu al</span>
        </button>` : ''}
    </div>
    <input id="dynInput" type="${inputType}" placeholder="${escapeHtml(sub.inputPlaceholder || '')}"
      class="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 focus:border-brand-500 focus:ring-2 focus:ring-brand-100 outline-none transition" />
    ${supportsLocation ? '<p id="locationStatus" class="mt-1 min-h-4 text-xs text-slate-500" aria-live="polite"></p>' : ''}`;
  const input = field.querySelector('#dynInput');
  input.value = getSuggestedLocation(activeCategory.serviceType);
  form.appendChild(field);

  // Submit button
  const btn = document.createElement('button');
  btn.id = 'calcBtn';
  btn.type = 'submit';
  btn.className = 'w-full py-2.5 rounded-xl bg-brand-600 hover:bg-brand-700 text-white font-medium transition shadow-sm disabled:opacity-60';
  btn.textContent = activeCategory.serviceType === 'weather'
    ? 'Hava Durumunu Göster'
    : activeCategory.serviceType === 'prayer'
      ? 'Namaz Vakitlerini Göster'
      : 'Hesapla';
  form.appendChild(btn);
  input.addEventListener('keydown', (event) => {
    if (event.key !== 'Enter' || event.isComposing) return;
    event.preventDefault();
    event.stopPropagation();
    void handleCalculate();
  });

  const resultSlot = document.createElement('div');
  resultSlot.style.minHeight = '68px';

  // Result area
  const result = document.createElement('div');
  result.id = 'dynResult';
  result.className = 'hidden rounded-xl bg-emerald-50 border border-emerald-200 px-4 py-3 text-emerald-800';
  resultSlot.appendChild(result);
  form.appendChild(resultSlot);

  panel.appendChild(form);
  input.focus({ preventScroll: true });
  const locationButton = form.querySelector('#locationPermissionBtn');
  if (locationButton) {
    locationButton.onclick = requestLocationAccess;
    if (detectedLocation) {
      form.querySelector('#locationStatus').textContent =
        `Bulunan konum: ${detectedLocation.city}, ${detectedLocation.district}. İsterseniz alanı değiştirebilirsiniz.`;
      form.querySelector('.location-button-label').textContent = 'Konumumu yenile';
    }
  }
}

async function handleCalculate() {
  if (!activeCategory || !activeSub) return;
  const btn = el('calcBtn');
  if (!btn || btn.disabled) return;
  const input = el('dynInput');
  const result = el('dynResult');

  const value = (input?.value || '').trim();
  if (!value) { toast('Lütfen bir değer girin.', 'error'); return; }

  const directionBtn = document.querySelector('#dynamicForm button[data-direction].bg-brand-50')
    || document.querySelector('#dynamicForm button[data-direction]');
  const direction = directionBtn ? directionBtn.dataset.direction : null;

  const payload = {
    serviceType: activeCategory.serviceType,
    subCategory: activeSub.key,
    inputValue: value,
    direction,
    city: activeSub.inputKind === 'text' ? value : null
  };
  if (isUsingDetectedLocation(value)) {
    payload.latitude = detectedLocation.latitude;
    payload.longitude = detectedLocation.longitude;
  }

  const calculationStartedAt = performance.now();
  const requestSessionVersion = authSessionVersion;
  btn.disabled = true;
  btn.textContent = 'Hesaplanıyor...';
  result.classList.add('hidden');
  try {
    const data = await api('/search/calculate', { method: 'POST', body: payload });
    if (requestSessionVersion !== authSessionVersion) return;
    if (data.success) {
      result.classList.remove('hidden');
      result.className = 'rounded-xl bg-emerald-50 border border-emerald-200 px-4 py-3 text-emerald-800';
      result.innerHTML = `
        <div class="text-sm font-medium">${escapeHtml(data.resultLabel || 'Sonuç')}: <span class="font-semibold">${escapeHtml(data.resultValue || '')}</span></div>
        ${data.rawDetail ? `<div class="text-xs text-emerald-700/80 mt-1">${escapeHtml(data.rawDetail)}</div>` : ''}`;
    } else {
      result.classList.remove('hidden');
      result.className = 'rounded-xl bg-red-50 border border-red-200 px-4 py-3 text-red-700';
      result.textContent = data.message || 'Hesaplama başarısız.';
    }
  } catch (err) {
    if (requestSessionVersion !== authSessionVersion) return;
    result.classList.remove('hidden');
    result.className = 'rounded-xl bg-red-50 border border-red-200 px-4 py-3 text-red-700';
    result.textContent = err.message;
  } finally {
    const remainingDisplayTime = MIN_CALCULATING_DISPLAY_MS - (performance.now() - calculationStartedAt);
    if (remainingDisplayTime > 0) {
      await new Promise((resolve) => setTimeout(resolve, remainingDisplayTime));
    }
    if (requestSessionVersion === authSessionVersion) {
      btn.disabled = false;
      btn.textContent = activeCategory?.serviceType === 'weather'
        ? 'Hava Durumunu Göster'
        : activeCategory?.serviceType === 'prayer'
          ? 'Namaz Vakitlerini Göster'
          : 'Hesapla';
    }
  }
}

function getSuggestedLocation(serviceType) {
  if (!detectedLocation) return '';
  if (serviceType === 'weather') return detectedLocation.city;
  if (serviceType === 'prayer') return `${detectedLocation.city}, ${detectedLocation.district}`;
  return '';
}

function isUsingDetectedLocation(value) {
  const suggestedLocation = activeCategory ? getSuggestedLocation(activeCategory.serviceType) : '';
  return !!suggestedLocation && value.localeCompare(suggestedLocation, 'tr', { sensitivity: 'accent' }) === 0;
}

async function requestLocationAccess() {
  if (locationRequestInProgress) return;

  const button = el('locationPermissionBtn');
  const status = el('locationStatus');
  if (!navigator.geolocation) {
    status.textContent = 'Bu tarayıcı konum özelliğini desteklemiyor. Konumu elle girebilirsiniz.';
    status.className = 'mt-1 text-xs font-medium text-amber-700';
    return;
  }

  locationRequestInProgress = true;
  const requestId = ++locationRequestId;
  const requestedServiceType = activeCategory?.serviceType;
  detectedLocation = null;
  if (el('dynInput')) el('dynInput').value = '';
  button.disabled = true;
  button.querySelector('.location-button-label').textContent = 'Konum alınıyor...';
  status.textContent = 'Tarayıcının konum izni penceresini onaylayabilirsin.';
  status.className = 'mt-1 text-xs font-medium text-slate-500';
  try {
    const position = await new Promise((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(resolve, reject, {
        enableHighAccuracy: true,
        timeout: 12000,
        maximumAge: 15 * 60 * 1000
      });
    });
    if (requestId !== locationRequestId) return;

    const location = await api('/search/location', {
      method: 'POST',
      body: {
        latitude: position.coords.latitude,
        longitude: position.coords.longitude
      }
    });
    if (requestId !== locationRequestId) return;

    detectedLocation = {
      city: location.city,
      district: location.district,
      latitude: position.coords.latitude,
      longitude: position.coords.longitude
    };

    if (activeCategory?.serviceType === requestedServiceType) {
      const input = el('dynInput');
      const newValue = getSuggestedLocation(requestedServiceType);
      if (input && newValue) {
        input.value = newValue;
      }

      const currentStatus = el('locationStatus');
      if (currentStatus) {
        currentStatus.textContent = `Bulunan konum: ${location.city}, ${location.district}. İsterseniz alanı değiştirebilirsiniz.`;
        currentStatus.className = 'mt-1 text-xs font-medium text-emerald-700';
        el('locationPermissionBtn').querySelector('.location-button-label').textContent = 'Konumumu yenile';
      }
      input?.focus({ preventScroll: true });
    }
  } catch (error) {
    const currentStatus = el('locationStatus');
    if (requestId !== locationRequestId) return;
    if (currentStatus && activeCategory?.serviceType === requestedServiceType) {
      if (error && error.code === 1) {
        currentStatus.textContent = 'Konum izni verilmedi. Tarayıcı ayarlarından izin verebilir veya şehrinizi elle girebilirsiniz.';
      } else if (error && error.code === 3) {
        currentStatus.textContent = 'Konum belirlenemedi. Tekrar deneyebilir veya şehrinizi elle girebilirsiniz.';
      } else {
        currentStatus.textContent = error instanceof Error
          ? `Konum alınamadı: ${error.message}`
          : 'Konum alınamadı. Şehrinizi elle girebilirsiniz.';
      }
      currentStatus.className = 'mt-1 text-xs font-medium text-amber-700';
    }
  } finally {
    if (requestId === locationRequestId) {
      locationRequestInProgress = false;
      const currentButton = el('locationPermissionBtn');
      if (currentButton) {
        currentButton.disabled = false;
        if (!detectedLocation) {
          currentButton.querySelector('.location-button-label').textContent = 'Tekrar dene';
        }
      }
    }
  }
}

// ---------- History ----------
el('sharedDataBtn').onclick = async () => {
  el('sharedDataModal').classList.remove('hidden');
  await loadUserSharedData();
};

el('sharedDataFilter').onclick = loadUserSharedData;
el('closeSharedData').onclick = () => el('sharedDataModal').classList.add('hidden');
el('sharedDataModal').addEventListener('click', (event) => {
  if (event.target === el('sharedDataModal')) el('sharedDataModal').classList.add('hidden');
});

async function loadUserSharedData() {
  const list = el('sharedDataList');
  const requestSessionVersion = authSessionVersion;
  list.innerHTML = '<div class="text-center text-slate-400 py-6">Yükleniyor...</div>';
  const query = buildSharedDataQuery('sharedDataType', 'sharedDataFrom', 'sharedDataTo');
  try {
    const records = await api(`/search/shared-data?${query}`);
    if (requestSessionVersion !== authSessionVersion) return;
    renderSharedDataInto(list, records, 'Bu filtrelerde ortak veri bulunamadı.');
  } catch (error) {
    if (requestSessionVersion !== authSessionVersion) return;
    list.innerHTML = `<div class="text-center text-red-600 py-6">${escapeHtml(error.message)}</div>`;
  }
}

el('historyBtn').onclick = async () => {
  el('historyModal').classList.remove('hidden');
  await loadPersonalHistory();
};

async function loadPersonalHistory() {
  const list = el('historyList');
  const requestVersion = ++historyRequestVersion;
  const sessionVersion = authSessionVersion;
  list.innerHTML = '<div class="text-center text-slate-400 py-8">Yükleniyor...</div>';
  try {
    const items = await api('/search/history');
    if (requestVersion !== historyRequestVersion || sessionVersion !== authSessionVersion) return;
    renderHistory(items);
  } catch (err) {
    if (requestVersion !== historyRequestVersion || sessionVersion !== authSessionVersion) return;
    list.innerHTML = `<div class="text-center text-red-600 py-6">${escapeHtml(err.message)}</div>`;
  }
}

el('clearHistoryBtn').onclick = async () => {
  if (!window.confirm('Kendi işlem geçmişiniz kalıcı olarak silinsin mi? Bu işlem ortak verileri veya diğer kullanıcıların geçmişini etkilemez.')) return;

  const button = el('clearHistoryBtn');
  const sessionVersion = authSessionVersion;
  historyRequestVersion++;
  button.disabled = true;
  button.textContent = 'Temizleniyor...';
  try {
    await api('/search/history', { method: 'DELETE' });
    if (sessionVersion !== authSessionVersion) return;
    el('historyList').innerHTML = '<div class="text-center text-slate-400 py-8">Henüz işlem geçmişiniz yok.</div>';
    toast('İşlem geçmişiniz temizlendi.', 'success');
  } catch (error) {
    if (sessionVersion !== authSessionVersion) return;
    el('historyList').innerHTML = `<div class="text-center text-red-600 py-6">${escapeHtml(error.message)}</div>`;
  } finally {
    if (sessionVersion === authSessionVersion) {
      button.disabled = false;
      button.textContent = 'Geçmişi temizle';
    }
  }
};

el('closeHistory').onclick = () => el('historyModal').classList.add('hidden');
el('historyModal').addEventListener('click', (e) => {
  if (e.target === el('historyModal')) el('historyModal').classList.add('hidden');
});

function buildSharedDataQuery(typeId, fromId, toId) {
  const params = new URLSearchParams({ take: '500' });
  const type = el(typeId).value;
  const from = el(fromId).value;
  const to = el(toId).value;
  if (type) params.set('type', type);
  if (from) params.set('from', from);
  if (to) params.set('to', to);
  return params.toString();
}

function renderSharedDataInto(list, records, emptyMessage, isAdmin = false) {
  if (!records || records.length === 0) {
    list.innerHTML = `<div class="text-center text-slate-400 py-6">${escapeHtml(emptyMessage)}</div>`;
    return;
  }

  list.innerHTML = '';
  records.forEach((record) => {
    let payload;
    try {
      payload = JSON.parse(record.payloadJson);
    } catch (error) {
      console.error('Arşiv kaydının JSON içeriği okunamadı.', error);
      payload = null;
    }

    list.appendChild(createSharedDataCard(record, payload, isAdmin));
  });
}

function createSharedDataCard(record, payload, isAdmin) {
    const row = document.createElement('article');
    row.className = 'rounded-xl border border-slate-200 p-3 space-y-2';
    if (isAdmin) {
      row.dataset.sharedId = record.id;
    }
    const details = isAdmin
      ? payload
        ? `<details><summary class="cursor-pointer text-xs text-slate-500">Veri ayrıntısı</summary><pre class="mt-2 overflow-x-auto rounded-lg bg-slate-50 p-2 text-xs">${escapeHtml(JSON.stringify(payload, null, 2))}</pre></details>`
        : '<div class="text-xs text-red-600">Bu kaydın JSON verisi okunamadı.</div>'
      : '';
    const managedStatus = record.isAdminManaged
      ? record.dataDate === getSharedDataDefaultDate(record.dataType)
        ? 'Yönetici düzenlemesi · Bugünkü hesaplamalarda kullanılır'
        : 'Yönetici düzenlemesi'
      : '';
    row.innerHTML = `
      <div class="flex flex-wrap items-start justify-between gap-2">
        <div class="min-w-0">
          <div class="font-medium text-sm text-slate-800">${escapeHtml(record.displayName)}</div>
          <div class="text-xs text-slate-500">${escapeHtml(sharedDataTypeLabel(record.dataType))} · ${escapeHtml(record.recordKey)} · ${escapeHtml(formatDataDate(record.dataDate))}</div>
          ${managedStatus ? `<div class="mt-1 text-xs text-amber-700">${escapeHtml(managedStatus)}</div>` : ''}
        </div>
      </div>
      <div class="text-sm text-slate-700">${escapeHtml(sharedDataSummary(record, payload))}</div>
      ${details}
      ${isAdmin ? `<div class="flex gap-2 pt-1"><button data-shared-action="edit" type="button" class="px-3 py-1.5 rounded-lg bg-slate-100 text-slate-700 text-xs">Düzenle</button><button data-shared-action="delete" type="button" class="px-3 py-1.5 rounded-lg bg-red-50 text-red-600 text-xs">Sil</button></div>` : ''}`;
    return row;
}

function sharedDataTypeLabel(type) {
  return ({
    currency: 'Döviz',
    gold: 'Altın',
    weather: 'Hava durumu',
    prayer: 'Namaz vakitleri'
  })[type] || type;
}

function sharedDataSummary(record, payload) {
  if (!payload) return 'Veri içeriği okunamadı.';
  if (record.dataType === 'currency') {
    return `${sharedDataField(payload, 'code') || record.recordKey} · Alış ${sharedDataField(payload, 'forexBuying')} · Satış ${sharedDataField(payload, 'forexSelling')} TL`;
  }
  if (record.dataType === 'gold') {
    const buyingPrice = sharedDataField(payload, 'buyingPrice');
    const sellingPrice = sharedDataField(payload, 'sellingPrice');
    const currency = sharedDataField(payload, 'currency') || 'TRY';
    return buyingPrice !== undefined && sellingPrice !== undefined
      ? `Alış ${buyingPrice} ${currency} · Satış ${sellingPrice} ${currency}`
      : `Birim fiyat: ${sharedDataField(payload, 'unitPrice')} TL`;
  }
  if (record.dataType === 'weather') {
    const weatherData = sharedDataField(payload, 'weatherData');
    const current = weatherData && sharedDataField(weatherData, 'current');
    const resultValue = sharedDataField(payload, 'resultValue');
    if (!current) return resultValue || 'Hava durumu bilgisi';
    return `${resultValue} · Sıcaklık ${sharedDataField(current, 'temperature_2m')}°C · Hissedilen ${sharedDataField(current, 'apparent_temperature')}°C · Nem %${sharedDataField(current, 'relative_humidity_2m')} · Rüzgar ${sharedDataField(current, 'wind_speed_10m')} km/sa · Kod ${sharedDataField(current, 'weather_code')}`;
  }
  if (record.dataType === 'prayer') {
    return `İmsak ${sharedDataField(payload, 'imsak')} · Güneş ${sharedDataField(payload, 'gunes')} · Öğle ${sharedDataField(payload, 'ogle')} · İkindi ${sharedDataField(payload, 'ikindi')} · Akşam ${sharedDataField(payload, 'aksam')} · Yatsı ${sharedDataField(payload, 'yatsi')}`;
  }
  return 'Ortak veri kaydı';
}

function sharedDataField(value, name) {
  const entry = Object.entries(value || {}).find(([key]) => key.toLowerCase() === name.toLowerCase());
  return entry ? entry[1] : undefined;
}

function formatDataDate(value) {
  const date = new Date(`${value}T00:00:00`);
  return isNaN(date.getTime()) ? value : date.toLocaleDateString('tr-TR');
}

function renderHistory(items) {
  renderHistoryInto(el('historyList'), items, 'Henüz işlem geçmişiniz yok.');
}

function renderHistoryInto(list, items, emptyMessage) {
  if (!items || items.length === 0) {
    list.innerHTML = `<div class="text-center text-slate-400 py-8">${escapeHtml(emptyMessage)}</div>`;
    return;
  }
  list.innerHTML = '';
  items.forEach((it) => {
    const row = document.createElement('div');
    row.className = 'flex items-start justify-between gap-3 p-3 rounded-xl border border-slate-200 hover:bg-slate-50 transition';
    const svcLabel = serviceLabel(it.serviceType, it.subCategory);
    row.innerHTML = `
      <div class="min-w-0">
        <div class="font-medium text-slate-800 text-sm">${escapeHtml(svcLabel)}</div>
        ${it.inputLabel ? `<div class="text-xs text-slate-500 mt-0.5">${escapeHtml(it.inputLabel)}: ${escapeHtml(it.inputValue || '')}</div>` : ''}
        ${it.resultLabel ? `<div class="text-xs text-emerald-700 mt-0.5">${escapeHtml(it.resultLabel)}: ${escapeHtml(it.resultValue || '')}</div>` : ''}
      </div>
      <div class="text-xs text-slate-400 whitespace-nowrap">${formatDate(it.timestamp)}</div>`;
    list.appendChild(row);
  });
}

function serviceLabel(svc, sub) {
  const cat = categories.find((c) => c.serviceType === svc);
  if (!cat) return `${svc} / ${sub}`;
  const s = cat.subCategories.find((x) => x.key === sub);
  return `${cat.displayName} / ${s ? s.displayName : sub}`;
}

function formatDate(ts) {
  const d = new Date(ts);
  if (isNaN(d.getTime())) return ts;
  return d.toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
}

function escapeHtml(s) {
  if (s == null) return '';
  return String(s).replace(/[&<>"']/g, (c) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  }[c]));
}

// ---------- Init ----------
if (isLoggedIn()) {
  enterApp();
} else {
  setMode('login');
}