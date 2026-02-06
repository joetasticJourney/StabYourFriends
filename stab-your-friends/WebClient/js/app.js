import { Connection } from './connection.js';
import { Controller } from './controller.js';

class App {
    constructor() {
        this.connection = new Connection();
        this.controller = new Controller();

        this.playerId = null;
        this.playerName = '';
        this.playerColor = '';
        this.gameStarted = false;

        this.screens = {
            connect: document.getElementById('connect-screen'),
            lobby: document.getElementById('lobby-screen'),
            controller: document.getElementById('controller-screen')
        };

        this.init();
    }

    init() {
        this.setupConnectionHandlers();
        this.setupConnectForm();
        this.setupLobbyControls();
        this.setupControllerScreen();
        this.controller.init();

        // Auto-fill server IP from current page host (since HTTP and WebSocket are on same server)
        const currentHost = window.location.hostname || 'localhost';
        document.getElementById('server-ip').value = currentHost;

        // Load saved connection info (override auto-fill if saved)
        const savedIp = localStorage.getItem('serverIp');
        const savedName = localStorage.getItem('playerName');
        if (savedIp) document.getElementById('server-ip').value = savedIp;
        if (savedName) document.getElementById('player-name').value = savedName;

        // Set cert-accept link (uses page hostname, always correct)
        this.updateCertLink();

        // Generate or load device ID for reconnection
        this.deviceId = localStorage.getItem('deviceId');
        if (!this.deviceId) {
            const arr = new Uint8Array(16);
            crypto.getRandomValues(arr);
            arr[6] = (arr[6] & 0x0f) | 0x40;
            arr[8] = (arr[8] & 0x3f) | 0x80;
            const hex = [...arr].map(b => b.toString(16).padStart(2, '0')).join('');
            this.deviceId = `${hex.slice(0,8)}-${hex.slice(8,12)}-${hex.slice(12,16)}-${hex.slice(16,20)}-${hex.slice(20)}`;
            localStorage.setItem('deviceId', this.deviceId);
        }
    }

    updateCertLink() {
        // Use the page's hostname directly — it's always the game server's IP
        const ip = window.location.hostname || 'localhost';
        const url = `https://${ip}:8443`;
        const link = document.getElementById('cert-accept-link');
        link.href = url;
        link.textContent = `Accept certificate: ${url}`;
    }

    setupControllerScreen() {
        const fullscreenBtn = document.getElementById('fullscreen-btn');

        // Hide button if already in standalone/fullscreen mode (launched from home screen)
        if (window.navigator.standalone || window.matchMedia('(display-mode: standalone)').matches) {
            fullscreenBtn.style.display = 'none';
            return;
        }

        fullscreenBtn.addEventListener('touchstart', (e) => {
            e.preventDefault();
            this.requestFullscreen();
        }, { passive: false });
        fullscreenBtn.addEventListener('mousedown', (e) => {
            e.preventDefault();
            this.requestFullscreen();
        });
    }

    setupConnectionHandlers() {
        this.connection.onConnected = () => {
            console.log('Connected to server');
        };

        this.connection.onDisconnected = (code, reason) => {
            console.log('Disconnected:', code, reason);
            this.handleDisconnect();
        };

        this.connection.onError = (error) => {
            console.error('Connection error:', error);
        };

        this.connection.onMessage = (message) => {
            this.handleMessage(message);
        };
    }

    setupConnectForm() {
        const form = document.getElementById('connect-form');
        const errorEl = document.getElementById('connect-error');
        const connectBtn = document.getElementById('connect-btn');

        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            errorEl.textContent = '';

            const name = document.getElementById('player-name').value.trim();
            const ip = document.getElementById('server-ip').value.trim();
            const port = document.getElementById('server-port').value || '8443';

            if (!name || !ip) {
                errorEl.textContent = 'Please enter your name and server IP';
                return;
            }

            this.playerName = name;
            localStorage.setItem('serverIp', ip);
            localStorage.setItem('playerName', name);

            // Init Web Audio during user gesture to unlock AudioContext on iOS
            this.controller.initAudio();

            connectBtn.disabled = true;
            connectBtn.textContent = 'Connecting...';

            try {
                await this.connection.connect(ip, port);
                this.connection.sendJoin(name, this.deviceId);
            } catch (error) {
                errorEl.textContent = error.message || 'Failed to connect';
                connectBtn.disabled = false;
                connectBtn.textContent = 'Connect';
            }
        });
    }

    setupLobbyControls() {
        const disconnectBtn = document.getElementById('disconnect-btn');

        disconnectBtn.addEventListener('click', () => {
            this.connection.disconnect();
        });
    }

    handleMessage(message) {
        switch (message.type) {
            case 'welcome':
                this.handleWelcome(message);
                break;
            case 'lobbyState':
                this.handleLobbyState(message);
                break;
            case 'error':
                this.handleError(message);
                break;
            case 'gameStart':
                this.handleGameStart(message);
                break;
            case 'grappleState':
                this.controller.setStabMode(message.stabSpeed);
                break;
            case 'playerState':
                this.controller.updatePlayerState(message);
                break;
            case 'gameEnd':
                this.handleGameEnd();
                break;
            case 'oof':
                console.log('Received oof message');
                this.controller.playOof();
                break;
            case 'death':
                console.log('Received death message');
                this.controller.playDeath();
                break;
        }
    }

    handleWelcome(message) {
        this.playerId = message.playerId;
        this.playerColor = message.playerColor;

        document.getElementById('player-display-name').textContent = this.playerName;
        document.getElementById('player-color-indicator').style.backgroundColor = '#' + this.playerColor;

        this.showScreen('lobby');

        // Reset connect button
        const connectBtn = document.getElementById('connect-btn');
        connectBtn.disabled = false;
        connectBtn.textContent = 'Connect';
    }

    handleLobbyState(message) {
        const playersList = document.getElementById('players-list');
        playersList.innerHTML = '';

        for (const player of message.players) {
            const item = document.createElement('div');
            item.className = 'player-item';
            item.innerHTML = `
                <div class="color-dot" style="background-color: #${player.color}"></div>
                <span class="name">${this.escapeHtml(player.name)}</span>
            `;
            playersList.appendChild(item);
        }

        const statusEl = document.getElementById('lobby-status');
        if (message.canStart) {
            statusEl.textContent = `${message.players.length} player(s) connected. Waiting for host to start...`;
        } else {
            statusEl.textContent = `Waiting for more players... (${message.players.length} connected)`;
        }
    }

    handleError(message) {
        console.error('Server error:', message.code, message.message);

        if (message.code === 'LOBBY_FULL' || message.code === 'ALREADY_JOINED') {
            document.getElementById('connect-error').textContent = message.message;
            this.connection.disconnect();
        }
    }

    handleGameStart(message) {
        console.log('Game starting! Mode:', message.gameMode);
        this.gameStarted = true;
        this.showScreen('controller');

        // Configure controller mode (undefined treated as true for backwards compatibility)
        this.controller.configureForControllerMode(message.controllerMode !== false);

        // Request fullscreen
        this.requestFullscreen();

        this.controller.onInputChange = (moveX, moveY, action1, action2, orientAlpha) => {
            this.connection.sendInput(moveX, moveY, action1, action2, orientAlpha);
        };
        this.controller.onShake = () => {
            this.connection.sendShake();
        };
        this.controller.startSendingInput();
    }

    handleGameEnd() {
        console.log('Game ended, returning to lobby');
        this.gameStarted = false;
        this.controller.stopSendingInput();
        this.controller.reset();
        this.exitFullscreen();
        this.showScreen('lobby');
    }

    requestFullscreen() {
        const elem = document.documentElement;

        if (elem.requestFullscreen) {
            elem.requestFullscreen().catch(err => console.log('Fullscreen error:', err));
        } else if (elem.webkitRequestFullscreen) {
            elem.webkitRequestFullscreen();
        } else if (elem.mozRequestFullScreen) {
            elem.mozRequestFullScreen();
        } else if (elem.msRequestFullscreen) {
            elem.msRequestFullscreen();
        } else {
            this.showIOSFullscreenTip();
            return;
        }

        // Also try to lock screen orientation to portrait
        if (screen.orientation && screen.orientation.lock) {
            screen.orientation.lock('portrait').catch(err => console.log('Orientation lock error:', err));
        }
    }

    showIOSFullscreenTip() {
        if (window.navigator.standalone) return;
        if (document.getElementById('ios-fullscreen-tip')) return;

        const tip = document.createElement('div');
        tip.id = 'ios-fullscreen-tip';
        tip.style.cssText = 'position:fixed;top:0;left:0;right:0;padding:14px 20px;background:rgba(233,69,96,0.95);color:#fff;text-align:center;font-size:0.9rem;z-index:9999;';
        tip.innerHTML = 'Open in <b>Safari</b> → tap <b>Share</b> → <b>Add to Home Screen</b> for fullscreen';

        const close = document.createElement('span');
        close.textContent = ' ✕';
        close.style.cssText = 'margin-left:10px;cursor:pointer;font-weight:bold;';
        close.addEventListener('touchstart', (e) => { e.preventDefault(); tip.remove(); }, { passive: false });
        close.addEventListener('mousedown', (e) => { e.preventDefault(); tip.remove(); });
        tip.appendChild(close);

        document.body.appendChild(tip);

        setTimeout(() => tip.remove(), 8000);
    }

    exitFullscreen() {
        if (document.exitFullscreen) {
            document.exitFullscreen().catch(() => {});
        } else if (document.webkitExitFullscreen) {
            document.webkitExitFullscreen();
        } else if (document.mozCancelFullScreen) {
            document.mozCancelFullScreen();
        } else if (document.msExitFullscreen) {
            document.msExitFullscreen();
        }
    }

    handleDisconnect() {
        this.playerId = null;
        this.playerColor = '';
        this.gameStarted = false;

        this.controller.stopSendingInput();
        this.controller.reset();
        this.exitFullscreen();
        this.showScreen('connect');

        const connectBtn = document.getElementById('connect-btn');
        connectBtn.disabled = false;
        connectBtn.textContent = 'Connect';
    }

    showScreen(screenName) {
        for (const [name, el] of Object.entries(this.screens)) {
            el.classList.toggle('active', name === screenName);
        }
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Initialize app when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.app = new App();
});
