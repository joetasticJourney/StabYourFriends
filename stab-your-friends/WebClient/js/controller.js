export class Controller {
    constructor() {
        this.touchpad = null;
        this.touchpadIndicator = null;
        this.action1Btn = null;
        this.action2Btn = null;

        this.moveX = 0;
        this.moveY = 0;
        this.action1 = false;
        this.action2 = false;

        this.touchpadActive = false;
        this.touchpadTouchId = null;
        this.touchpadRect = null;

        this.onInputChange = null;
        this.inputSendInterval = null;
        this.inputSendRate = 1000 / 30; // 30Hz
    }

    init() {
        this.touchpad = document.getElementById('touchpad');
        this.touchpadIndicator = document.getElementById('touchpad-indicator');
        this.action1Btn = document.getElementById('action1-btn');
        this.action2Btn = document.getElementById('action2-btn');

        this.setupTouchpad();
        this.setupButtons();
    }

    setupTouchpad() {
        // Touch events
        this.touchpad.addEventListener('touchstart', (e) => this.onTouchpadStart(e), { passive: false });
        this.touchpad.addEventListener('touchmove', (e) => this.onTouchpadMove(e), { passive: false });
        this.touchpad.addEventListener('touchend', (e) => this.onTouchpadEnd(e), { passive: false });
        this.touchpad.addEventListener('touchcancel', (e) => this.onTouchpadEnd(e), { passive: false });

        // Mouse events for desktop testing
        this.touchpad.addEventListener('mousedown', (e) => this.onMouseDown(e));
        window.addEventListener('mousemove', (e) => this.onMouseMove(e));
        window.addEventListener('mouseup', (e) => this.onMouseUp(e));
    }

    onTouchpadStart(e) {
        e.preventDefault();
        if (this.touchpadActive) return;

        const touch = e.changedTouches[0];
        this.touchpadTouchId = touch.identifier;
        this.touchpadActive = true;
        this.touchpadRect = this.touchpad.getBoundingClientRect();

        this.updateTouchpadPosition(touch.clientX, touch.clientY);
    }

    onTouchpadMove(e) {
        e.preventDefault();
        if (!this.touchpadActive) return;

        for (const touch of e.changedTouches) {
            if (touch.identifier === this.touchpadTouchId) {
                this.updateTouchpadPosition(touch.clientX, touch.clientY);
                break;
            }
        }
    }

    onTouchpadEnd(e) {
        e.preventDefault();
        for (const touch of e.changedTouches) {
            if (touch.identifier === this.touchpadTouchId) {
                this.resetTouchpad();
                break;
            }
        }
    }

    onMouseDown(e) {
        this.touchpadActive = true;
        this.touchpadRect = this.touchpad.getBoundingClientRect();
        this.updateTouchpadPosition(e.clientX, e.clientY);
    }

    onMouseMove(e) {
        if (this.touchpadActive) {
            this.updateTouchpadPosition(e.clientX, e.clientY);
        }
    }

    onMouseUp(e) {
        if (this.touchpadActive) {
            this.resetTouchpad();
        }
    }

    updateTouchpadPosition(clientX, clientY) {
        if (!this.touchpadRect) return;

        // Calculate center of touchpad
        const centerX = this.touchpadRect.left + this.touchpadRect.width / 2;
        const centerY = this.touchpadRect.top + this.touchpadRect.height / 2;

        // Calculate offset from center
        let dx = clientX - centerX;
        let dy = clientY - centerY;

        // Normalize to -1 to 1 range based on half the touchpad size
        const maxRadius = Math.min(this.touchpadRect.width, this.touchpadRect.height) / 2;

        this.moveX = Math.max(-1, Math.min(1, dx / maxRadius));
        this.moveY = Math.max(-1, Math.min(1, dy / maxRadius));

        // Update indicator position (clamp to touchpad bounds)
        const indicatorX = Math.max(-maxRadius, Math.min(maxRadius, dx));
        const indicatorY = Math.max(-maxRadius, Math.min(maxRadius, dy));

        this.touchpadIndicator.style.transform = `translate(${indicatorX}px, ${indicatorY}px)`;
        this.touchpadIndicator.classList.add('active');
    }

    resetTouchpad() {
        this.touchpadActive = false;
        this.touchpadTouchId = null;
        this.moveX = 0;
        this.moveY = 0;
        this.touchpadIndicator.style.transform = 'translate(0px, 0px)';
        this.touchpadIndicator.classList.remove('active');
    }

    setupButtons() {
        // Action 1
        this.addButtonListeners(this.action1Btn, () => {
            this.action1 = true;
        }, () => {
            this.action1 = false;
        });

        // Action 2
        this.addButtonListeners(this.action2Btn, () => {
            this.action2 = true;
        }, () => {
            this.action2 = false;
        });
    }

    addButtonListeners(element, onPress, onRelease) {
        if (!element) return;

        // Touch events
        element.addEventListener('touchstart', (e) => {
            e.preventDefault();
            element.classList.add('pressed');
            onPress();
        }, { passive: false });

        element.addEventListener('touchend', (e) => {
            e.preventDefault();
            element.classList.remove('pressed');
            onRelease();
        }, { passive: false });

        element.addEventListener('touchcancel', (e) => {
            e.preventDefault();
            element.classList.remove('pressed');
            onRelease();
        }, { passive: false });

        // Mouse events for desktop testing
        element.addEventListener('mousedown', (e) => {
            e.preventDefault();
            element.classList.add('pressed');
            onPress();
        });

        element.addEventListener('mouseup', (e) => {
            element.classList.remove('pressed');
            onRelease();
        });

        element.addEventListener('mouseleave', (e) => {
            element.classList.remove('pressed');
            onRelease();
        });
    }

    startSendingInput() {
        if (this.inputSendInterval) return;

        this.inputSendInterval = setInterval(() => {
            if (this.onInputChange) {
                this.onInputChange(this.moveX, this.moveY, this.action1, this.action2);
            }
        }, this.inputSendRate);
    }

    stopSendingInput() {
        if (this.inputSendInterval) {
            clearInterval(this.inputSendInterval);
            this.inputSendInterval = null;
        }
    }

    reset() {
        this.resetTouchpad();
        this.action1 = false;
        this.action2 = false;
    }

    getInput() {
        return {
            moveX: this.moveX,
            moveY: this.moveY,
            action1: this.action1,
            action2: this.action2
        };
    }
}
