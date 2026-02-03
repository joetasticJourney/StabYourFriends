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
    }

    setupControllerScreen() {
        const fullscreenBtn = document.getElementById('fullscreen-btn');
        fullscreenBtn.addEventListener('click', () => {
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
            const port = document.getElementById('server-port').value || '9443';

            if (!name || !ip) {
                errorEl.textContent = 'Please enter your name and server IP';
                return;
            }

            this.playerName = name;
            localStorage.setItem('serverIp', ip);
            localStorage.setItem('playerName', name);

            connectBtn.disabled = true;
            connectBtn.textContent = 'Connecting...';

            try {
                await this.connection.connect(ip, port);
                this.connection.sendJoin(name);
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

        // Request fullscreen
        this.requestFullscreen();

        this.controller.onInputChange = (moveX, moveY, action1, action2) => {
            this.connection.sendInput(moveX, moveY, action1, action2);
        };
        this.controller.onShake = () => {
            this.connection.sendShake();
        };
        this.controller.startSendingInput();
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
        }

        // Also try to lock screen orientation to portrait
        if (screen.orientation && screen.orientation.lock) {
            screen.orientation.lock('portrait').catch(err => console.log('Orientation lock error:', err));
        }
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
