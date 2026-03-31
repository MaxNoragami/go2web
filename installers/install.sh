#!/usr/bin/env bash
set -e

OS="$(uname -s)"
BINARY_NAME="go2web"

echo "Installing $BINARY_NAME for $OS..."

# Target directory
INSTALL_DIR="$HOME/.local/bin"
mkdir -p "$INSTALL_DIR"

# Copy binary to target directory
cp "$BINARY_NAME" "$INSTALL_DIR/$BINARY_NAME"
chmod +x "$INSTALL_DIR/$BINARY_NAME"

echo "Successfully copied $BINARY_NAME to $INSTALL_DIR"

# Check if INSTALL_DIR is in PATH
if echo "$PATH" | grep -q "$INSTALL_DIR"; then
    echo "$INSTALL_DIR is already in your PATH."
else
    echo "Adding $INSTALL_DIR to your PATH..."
    
    # Detect the shell to update the correct profile
    USER_SHELL=$(basename "$SHELL")
    
    if [ "$USER_SHELL" = "zsh" ]; then
        echo "export PATH=\"$INSTALL_DIR:\$PATH\"" >> "$HOME/.zshrc"
        echo "Added to ~/.zshrc. Please run 'source ~/.zshrc' or restart your terminal."
    elif [ "$USER_SHELL" = "bash" ]; then
        if [ -f "$HOME/.bashrc" ]; then
            echo "export PATH=\"$INSTALL_DIR:\$PATH\"" >> "$HOME/.bashrc"
            echo "Added to ~/.bashrc. Please run 'source ~/.bashrc' or restart your terminal."
        elif [ -f "$HOME/.bash_profile" ]; then
            echo "export PATH=\"$INSTALL_DIR:\$PATH\"" >> "$HOME/.bash_profile"
            echo "Added to ~/.bash_profile. Please run 'source ~/.bash_profile' or restart your terminal."
        else
            echo "export PATH=\"$INSTALL_DIR:\$PATH\"" >> "$HOME/.profile"
            echo "Added to ~/.profile. Please run 'source ~/.profile' or restart your terminal."
        fi
    elif [ "$USER_SHELL" = "fish" ]; then
        if command -v fish >/dev/null 2>&1; then
            fish -c "set -U fish_user_paths $INSTALL_DIR \$fish_user_paths"
            echo "Added to fish_user_paths. You can use it immediately in new shells."
        else
            echo "Fish shell detected but 'fish' command not found. Add $INSTALL_DIR to your PATH manually."
        fi
    else
        echo "Could not automatically determine your shell (detected: $USER_SHELL)."
        echo "Please manually add $INSTALL_DIR to your PATH."
    fi
fi

echo "Installation complete! Open a new terminal and type 'go2web -h' to get started."
