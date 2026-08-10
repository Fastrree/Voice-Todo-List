const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { URL } = require('url');

const PORT = 5174;
const HOST = '127.0.0.1';
const DATA_DIR = path.join(__dirname, 'data');
const UPLOAD_DIR = path.join(__dirname, 'uploads');
const USERS_FILE = path.join(DATA_DIR, 'users.json');
const TODOS_FILE = path.join(DATA_DIR, 'todos.json');
const VOICE_FILE = path.join(DATA_DIR, 'voice.json');
const PROFILES_FILE = path.join(DATA_DIR, 'profiles.json');

function ensureDirs() {
  [DATA_DIR, UPLOAD_DIR].forEach((d) => { if (!fs.existsSync(d)) fs.mkdirSync(d, { recursive: true }); });
  [USERS_FILE, TODOS_FILE, VOICE_FILE, PROFILES_FILE].forEach((f) => {
    if (!fs.existsSync(f)) fs.writeFileSync(f, JSON.stringify([]));
  });
}

function readJson(file) {
  try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch { return []; }
}

function writeJson(file, data) {
  fs.writeFileSync(file, JSON.stringify(data, null, 2));
}

function hashPassword(password, salt) {
  return crypto.scryptSync(password, salt, 64).toString('hex');
}

function newSalt() { return crypto.randomBytes(16).toString('hex'); }

function newToken() { return crypto.randomBytes(32).toString('hex'); }

function newId() { return crypto.randomUUID(); }

function isBase64DataUrl(str) {
  return typeof str === 'string' && str.startsWith('data:audio/') && str.includes(';base64,');
}

function decodeAudioDataUrl(str) {
  const idx = str.indexOf(';base64,');
  const mime = str.slice(5, idx);
  const b64 = str.slice(idx + 8);
  return { mime, buffer: Buffer.from(b64, 'base64') };
}

function sendJson(res, status, obj) {
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8' });
  res.end(JSON.stringify(obj));
}

function sendError(res, status, code, message) {
  sendJson(res, status, { success: false, error: { code, message } });
}

function publicUrl(req, filename) {
  return `http://${req.headers.host || `${HOST}:${PORT}`}/uploads/${filename}`;
}

// --- Auth helpers ---
function findUserByEmail(email) {
  return readJson(USERS_FILE).find((u) => u.email.toLowerCase() === (email || '').toLowerCase());
}

function findUserByToken(token) {
  if (!token) return null;
  return readJson(USERS_FILE).find((u) => u.sessionToken === token);
}

function getBearerToken(req) {
  const h = req.headers['authorization'] || '';
  if (!h.startsWith('Bearer ')) return null;
  return h.slice(7).trim();
}

function toPublicUser(u) {
  return { id: u.id, email: u.email, created_at: u.createdAt };
}

function requireAuth(req, res, next) {
  const user = findUserByToken(getBearerToken(req));
  if (!user) return sendError(res, 401, 'UNAUTHORIZED', 'Geçersiz oturum. Lütfen tekrar giriş yapın.');
  req.user = user;
  next();
}

// --- Todo helpers ---
function todoForUser(todos, userId) { return todos.filter((t) => t.userId === userId); }

function toPublicTodo(t) {
  return {
    id: t.id,
    user_id: t.userId,
    title: t.title,
    description: t.description,
    completed: t.completed,
    voice_recording_url: t.voiceRecordingUrl,
    voice_duration: t.voiceDuration,
    priority: t.priority,
    due_date: t.dueDate,
    created_at: t.createdAt,
    updated_at: t.updatedAt,
  };
}

// --- Request body parsing ---
function readBody(req) {
  return new Promise((resolve, reject) => {
    let body = '';
    req.on('data', (c) => { body += c; if (body.length > 100 * 1024 * 1024) req.destroy(); });
    req.on('end', () => {
      if (!body) return resolve({});
      try { resolve(JSON.parse(body)); } catch { reject(new Error('Geçersiz JSON')); }
    });
    req.on('error', reject);
  });
}

// --- Routes ---
async function handle(req, res) {
  const url = new URL(req.url, `http://${HOST}:${PORT}`);
  const pathname = url.pathname;

  // Static uploads
  if (pathname.startsWith('/uploads/')) {
    const file = path.basename(pathname);
    const full = path.join(UPLOAD_DIR, file);
    if (!fs.existsSync(full)) return sendError(res, 404, 'NOT_FOUND', 'Dosya bulunamadı');
    res.writeHead(200, { 'Content-Type': 'audio/wav' });
    return fs.createReadStream(full).pipe(res);
  }

  // CORS
  if (req.method === 'OPTIONS') {
    res.writeHead(204, {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET,POST,PUT,DELETE,OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type, Authorization',
    });
    return res.end();
  }

  try {
    // --- AUTH ---
    if (pathname === '/auth/signup' && req.method === 'POST') {
      const body = await readBody(req);
      const { email, password } = body;
      if (!email || !password) return sendError(res, 400, 'VALIDATION_ERROR', 'E-posta ve şifre gerekli.');
      if (password.length < 6) return sendError(res, 400, 'VALIDATION_ERROR', 'Şifre en az 6 karakter olmalıdır.');
      if (findUserByEmail(email)) return sendError(res, 409, 'EMAIL_EXISTS', 'Bu e-posta adresi zaten kayıtlı.');

      const users = readJson(USERS_FILE);
      const salt = newSalt();
      const user = {
        id: newId(),
        email: email.trim().toLowerCase(),
        salt,
        passwordHash: hashPassword(password, salt),
        sessionToken: newToken(),
        createdAt: new Date().toISOString(),
      };
      users.push(user);
      writeJson(USERS_FILE, users);

      return sendJson(res, 200, { success: true, user: toPublicUser(user), access_token: user.sessionToken });
    }

    if (pathname === '/auth/signin' && req.method === 'POST') {
      const body = await readBody(req);
      const { email, password } = body;
      const user = findUserByEmail(email);
      if (!user || user.passwordHash !== hashPassword(password || '', user.salt)) {
        return sendError(res, 401, 'INVALID_CREDENTIALS', 'E-posta veya şifre hatalı.');
      }
      user.sessionToken = newToken();
      const users = readJson(USERS_FILE);
      const idx = users.findIndex((u) => u.id === user.id);
      if (idx >= 0) users[idx] = user;
      writeJson(USERS_FILE, users);

      return sendJson(res, 200, { success: true, user: toPublicUser(user), access_token: user.sessionToken });
    }

    if (pathname === '/auth/signout' && req.method === 'POST') {
      const user = findUserByToken(getBearerToken(req));
      if (user) {
        user.sessionToken = null;
        const users = readJson(USERS_FILE);
        const idx = users.findIndex((u) => u.id === user.id);
        if (idx >= 0) users[idx] = user;
        writeJson(USERS_FILE, users);
      }
      return sendJson(res, 200, { success: true });
    }

    if (pathname === '/auth/me' && req.method === 'GET') {
      const user = findUserByToken(getBearerToken(req));
      if (!user) return sendError(res, 401, 'UNAUTHORIZED', 'Geçersiz oturum.');
      return sendJson(res, 200, { success: true, user: toPublicUser(user) });
    }

    // --- PROTECTED ROUTES ---
    await new Promise((resolve) => requireAuth(req, res, () => resolve()));
    if (!req.user) return;

    // --- TODOS ---
    if (pathname === '/todos' && req.method === 'POST') {
      const body = await readBody(req);
      const now = new Date().toISOString();
      const todo = {
        id: newId(),
        userId: req.user.id,
        title: (body.title || '').trim(),
        description: body.description || null,
        completed: !!body.completed,
        voiceRecordingUrl: body.voiceUrl || null,
        voiceDuration: body.voiceDuration || null,
        priority: body.priority || 'medium',
        dueDate: body.dueDate || null,
        createdAt: now,
        updatedAt: now,
      };
      if (!todo.title) return sendError(res, 400, 'VALIDATION_ERROR', 'Başlık gerekli.');
      const todos = readJson(TODOS_FILE);
      todos.push(todo);
      writeJson(TODOS_FILE, todos);
      return sendJson(res, 200, { success: true, todo: toPublicTodo(todo) });
    }

    if (pathname === '/todos' && req.method === 'GET') {
      const todos = todoForUser(readJson(TODOS_FILE), req.user.id);
      const completedParam = url.searchParams.get('completed');
      let filtered = todos;
      if (completedParam === 'true') filtered = filtered.filter((t) => t.completed);
      if (completedParam === 'false') filtered = filtered.filter((t) => !t.completed);
      filtered.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
      return sendJson(res, 200, { success: true, todos: filtered.map(toPublicTodo) });
    }

    const todoMatch = pathname.match(/^\/todos\/([^/]+)$/);
    if (todoMatch) {
      const todoId = todoMatch[1];
      const todos = readJson(TODOS_FILE);
      const idx = todos.findIndex((t) => t.id === todoId && t.userId === req.user.id);

      if (req.method === 'GET') {
        if (idx < 0) return sendError(res, 404, 'NOT_FOUND', 'Görev bulunamadı');
        const recordings = readJson(VOICE_FILE).filter((v) => v.todoId === todoId && v.userId === req.user.id);
        recordings.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
        return sendJson(res, 200, {
          success: true,
          todo: toPublicTodo(todos[idx]),
          voiceRecordings: recordings.map(toPublicVoice),
        });
      }

      if (req.method === 'PUT') {
        if (idx < 0) return sendError(res, 404, 'NOT_FOUND', 'Görev bulunamadı');
        const body = await readBody(req);
        const t = todos[idx];
        if (body.title !== undefined) t.title = String(body.title).trim();
        if (body.description !== undefined) t.description = body.description;
        if (body.completed !== undefined) t.completed = !!body.completed;
        if (body.priority !== undefined) t.priority = String(body.priority);
        if (body.dueDate !== undefined) t.dueDate = body.dueDate;
        if (body.voice_recording_url !== undefined) t.voiceRecordingUrl = body.voice_recording_url;
        if (body.voice_duration !== undefined) t.voiceDuration = body.voice_duration;
        t.updatedAt = new Date().toISOString();
        todos[idx] = t;
        writeJson(TODOS_FILE, todos);
        return sendJson(res, 200, { success: true, todo: toPublicTodo(t) });
      }

      if (req.method === 'DELETE') {
        if (idx < 0) return sendError(res, 404, 'NOT_FOUND', 'Görev bulunamadı');
        const voice = readJson(VOICE_FILE);
        const removedVoice = voice.filter((v) => v.todoId !== todoId);
        writeJson(VOICE_FILE, removedVoice);
        todos.splice(idx, 1);
        writeJson(TODOS_FILE, todos);
        return sendJson(res, 200, { success: true, operation: 'deleted' });
      }
    }

    // --- VOICE ---
    if (pathname === '/voice' && req.method === 'POST') {
      const body = await readBody(req);
      if (!isBase64DataUrl(body.audioData)) {
        return sendError(res, 400, 'VALIDATION_ERROR', 'Geçersiz ses verisi.');
      }
      const { mime, buffer } = decodeAudioDataUrl(body.audioData);
      const ext = mime.includes('mpeg') || mime.includes('mp3') ? 'mp3' : 'wav';
      const filename = `${newId()}.${ext}`;
      fs.writeFileSync(path.join(UPLOAD_DIR, filename), buffer);

      const recording = {
        id: newId(),
        todoId: body.todoId || null,
        userId: req.user.id,
        fileName: body.fileName || filename,
        fileUrl: publicUrl(req, filename),
        storagePath: filename,
        fileSize: buffer.length,
        duration: body.duration || null,
        mimeType: mime,
        createdAt: new Date().toISOString(),
      };
      const voice = readJson(VOICE_FILE);
      voice.push(recording);
      writeJson(VOICE_FILE, voice);

      return sendJson(res, 200, {
        success: true,
        voiceRecording: toPublicVoice(recording),
        publicUrl: recording.fileUrl,
        storagePath: filename,
      });
    }

    // --- PROFILE ---
    if (pathname === '/profile' && req.method === 'POST') {
      const body = await readBody(req);
      const profiles = readJson(PROFILES_FILE);
      let profile = profiles.find((p) => p.userId === req.user.id);
      const now = new Date().toISOString();

      const profileData = body.profileData || {};
      const op = body.operation || 'get_or_create';

      if (op === 'update') {
        if (!profile) return sendError(res, 404, 'NOT_FOUND', 'Profil bulunamadı');
        if (profileData.fullName !== undefined) profile.fullName = profileData.fullName;
        if (profileData.preferences !== undefined) profile.preferences = profileData.preferences;
        profile.updatedAt = now;
        const pIdx = profiles.findIndex((p) => p.userId === req.user.id);
        profiles[pIdx] = profile;
        writeJson(PROFILES_FILE, profiles);
      } else {
        if (!profile) {
          profile = {
            id: newId(),
            userId: req.user.id,
            email: req.user.email,
            fullName: profileData.fullName || null,
            avatarUrl: null,
            preferences: profileData.preferences || {},
            createdAt: now,
            updatedAt: now,
          };
          profiles.push(profile);
        } else {
          if (profileData.fullName !== undefined) profile.fullName = profileData.fullName;
          if (profileData.preferences !== undefined) profile.preferences = profileData.preferences;
        }
        writeJson(PROFILES_FILE, profiles);
      }

      return sendJson(res, 200, { success: true, profile: toPublicProfile(profile) });
    }

    if (pathname === '/profile/stats' && req.method === 'GET') {
      const todos = todoForUser(readJson(TODOS_FILE), req.user.id);
      const voice = readJson(VOICE_FILE).filter((v) => v.userId === req.user.id);
      const completed = todos.filter((t) => t.completed);
      const now = new Date();
      const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
      const thisWeek = todos.filter((t) => new Date(t.createdAt) >= weekAgo);
      const totalDuration = voice.reduce((s, v) => s + (v.duration || 0), 0);

      return sendJson(res, 200, {
        success: true,
        stats: {
          total_todos: todos.length,
          completed_todos: completed.length,
          pending_todos: todos.length - completed.length,
          todos_with_voice: todos.filter((t) => t.voiceRecordingUrl).length,
          total_voice_recordings: voice.length,
          total_voice_duration: totalDuration,
          todos_this_week: thisWeek.length,
          completion_rate: todos.length > 0 ? Math.round((completed.length / todos.length) * 100) : 0,
        },
      });
    }

    return sendError(res, 404, 'NOT_FOUND', `Endpoint bulunamadı: ${req.method} ${pathname}`);
  } catch (err) {
    console.error('Server error:', err);
    sendError(res, 500, 'SERVER_ERROR', err.message || 'Sunucu hatası');
  }
}

function toPublicVoice(v) {
  return {
    id: v.id,
    todo_id: v.todoId,
    user_id: v.userId,
    file_url: v.fileUrl,
    file_name: v.fileName,
    file_size: v.fileSize,
    duration: v.duration,
    mime_type: v.mimeType,
    created_at: v.createdAt,
  };
}

function toPublicProfile(p) {
  return {
    id: p.id,
    email: p.email,
    full_name: p.fullName,
    avatar_url: p.avatarUrl,
    preferences: p.preferences || {},
    created_at: p.createdAt,
    updated_at: p.updatedAt,
  };
}

const server = http.createServer((req, res) => {
  const defaultHeaders = {
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Methods': 'GET,POST,PUT,DELETE,OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type, Authorization',
  };
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET,POST,PUT,DELETE,OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
  handle(req, res);
});

ensureDirs();
server.listen(PORT, HOST, () => {
  console.log(`TodoVoice lokal sunucu çalışıyor: http://${HOST}:${PORT}`);
  console.log(`Veri dizini: ${DATA_DIR}`);
});
