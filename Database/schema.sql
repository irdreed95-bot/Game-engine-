-- UNITED STATE | RP Database Schema
-- Optimized for 500 concurrent players
-- PostgreSQL 13+

-- Drop existing objects (if any)
DROP TABLE IF EXISTS chat_logs CASCADE;
DROP TABLE IF EXISTS character_inventory CASCADE;
DROP TABLE IF EXISTS faction_members CASCADE;
DROP TABLE IF EXISTS items CASCADE;
DROP TABLE IF EXISTS world_state CASCADE;
DROP TABLE IF EXISTS vehicles CASCADE;
DROP TABLE IF EXISTS sessions CASCADE;
DROP TABLE IF EXISTS characters CASCADE;
DROP TABLE IF EXISTS accounts CASCADE;
DROP TABLE IF EXISTS factions CASCADE;

-- ==================== Factions Table ====================
CREATE TABLE factions (
    id SERIAL PRIMARY KEY,
    name VARCHAR(64) UNIQUE NOT NULL,
    description TEXT,
    color VARCHAR(7),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO factions (name, description, color) VALUES
('Police', 'Law Enforcement', '#0000FF'),
('Fire', 'Emergency Services', '#FF6600'),
('Hospital', 'Medical Services', '#FF0000'),
('Taxi', 'Transportation Service', '#FFFF00'),
('Civilian', 'Regular Citizen', '#FFFFFF');

-- ==================== Accounts Table ====================
CREATE TABLE accounts (
    id SERIAL PRIMARY KEY,
    username VARCHAR(32) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    email VARCHAR(64) UNIQUE NOT NULL,
    status VARCHAR(16) DEFAULT 'active', -- active, suspended, banned
    last_login TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_accounts_username ON accounts(username);
CREATE INDEX idx_accounts_email ON accounts(email);
CREATE INDEX idx_accounts_status ON accounts(status);

-- ==================== Sessions Table ====================
CREATE TABLE sessions (
    id SERIAL PRIMARY KEY,
    account_id INTEGER NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    session_token VARCHAR(255) UNIQUE NOT NULL,
    character_id INTEGER,
    ip_address VARCHAR(45),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP NOT NULL,
    last_activity TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_active BOOLEAN DEFAULT true
);

CREATE INDEX idx_sessions_account_id ON sessions(account_id);
CREATE INDEX idx_sessions_token ON sessions(session_token);
CREATE INDEX idx_sessions_expires ON sessions(expires_at);

-- ==================== Characters Table ====================
CREATE TABLE characters (
    id SERIAL PRIMARY KEY,
    account_id INTEGER NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    name VARCHAR(32) UNIQUE NOT NULL,
    faction_id INTEGER REFERENCES factions(id),
    job VARCHAR(32) DEFAULT 'Civilian',
    health INT DEFAULT 100 CHECK(health >= 0 AND health <= 100),
    armor INT DEFAULT 0 CHECK(armor >= 0 AND armor <= 100),
    money INT DEFAULT 5000,
    bank_balance INT DEFAULT 0,
    position_x FLOAT DEFAULT 0.0,
    position_y FLOAT DEFAULT 0.0,
    position_z FLOAT DEFAULT 0.0,
    rotation_x FLOAT DEFAULT 0.0,
    rotation_y FLOAT DEFAULT 0.0,
    rotation_z FLOAT DEFAULT 0.0,
    playtime_seconds INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_seen TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_characters_account_id ON characters(account_id);
CREATE INDEX idx_characters_name ON characters(name);
CREATE INDEX idx_characters_faction_id ON characters(faction_id);

-- ==================== Items Table ====================
CREATE TABLE items (
    id SERIAL PRIMARY KEY,
    name VARCHAR(64) NOT NULL,
    item_type VARCHAR(32), -- weapon, food, tool, misc
    description TEXT,
    weight FLOAT DEFAULT 1.0,
    max_stack INT DEFAULT 1,
    properties JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO items (name, item_type, description, weight, max_stack) VALUES
('Pistol', 'weapon', 'Standard issue pistol', 2.5, 1),
('Flashlight', 'tool', 'Battery-powered flashlight', 0.5, 1),
('Phone', 'misc', 'Mobile phone', 0.2, 1),
('Money Bag', 'misc', 'Stack of cash', 0.1, 999),
('First Aid Kit', 'tool', 'Medical supplies', 1.0, 5);

CREATE INDEX idx_items_type ON items(item_type);

-- ==================== Character Inventory Table ====================
CREATE TABLE character_inventory (
    id SERIAL PRIMARY KEY,
    character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
    item_id INTEGER NOT NULL REFERENCES items(id),
    amount INT DEFAULT 1 CHECK(amount > 0),
    slot INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_inventory_character ON character_inventory(character_id);
CREATE INDEX idx_inventory_slot ON character_inventory(character_id, slot);

-- ==================== Vehicles Table ====================
CREATE TABLE vehicles (
    id SERIAL PRIMARY KEY,
    owner_id INTEGER REFERENCES accounts(id) ON DELETE SET NULL,
    vehicle_type VARCHAR(32), -- car, truck, motorcycle, etc.
    model VARCHAR(64),
    plate VARCHAR(16) UNIQUE,
    position_x FLOAT DEFAULT 0.0,
    position_y FLOAT DEFAULT 0.0,
    position_z FLOAT DEFAULT 0.0,
    rotation_x FLOAT DEFAULT 0.0,
    rotation_y FLOAT DEFAULT 0.0,
    rotation_z FLOAT DEFAULT 0.0,
    health INT DEFAULT 100,
    fuel FLOAT DEFAULT 100.0,
    is_parked BOOLEAN DEFAULT true,
    driver_id INTEGER REFERENCES characters(id) ON DELETE SET NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_vehicles_owner ON vehicles(owner_id);
CREATE INDEX idx_vehicles_plate ON vehicles(plate);
CREATE INDEX idx_vehicles_driver ON vehicles(driver_id);

-- ==================== Faction Members Table ====================
CREATE TABLE faction_members (
    id SERIAL PRIMARY KEY,
    character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
    faction_id INTEGER NOT NULL REFERENCES factions(id) ON DELETE CASCADE,
    rank VARCHAR(32) DEFAULT 'Member',
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(character_id, faction_id)
);

CREATE INDEX idx_faction_members_character ON faction_members(character_id);
CREATE INDEX idx_faction_members_faction ON faction_members(faction_id);

-- ==================== World State Table ====================
CREATE TABLE world_state (
    id SERIAL PRIMARY KEY,
    key VARCHAR(64) UNIQUE NOT NULL,
    value JSONB NOT NULL,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by VARCHAR(64)
);

INSERT INTO world_state (key, value, updated_by) VALUES
('time_hour', '12', 'system'),
('time_minute', '0', 'system'),
('weather', '"clear"', 'system'),
('temperature', '20', 'system');

CREATE INDEX idx_world_state_key ON world_state(key);

-- ==================== Chat Logs Table ====================
CREATE TABLE chat_logs (
    id SERIAL PRIMARY KEY,
    sender_id INTEGER REFERENCES characters(id) ON DELETE SET NULL,
    sender_name VARCHAR(32),
    receiver_id INTEGER REFERENCES characters(id) ON DELETE SET NULL,
    message TEXT NOT NULL,
    chat_type VARCHAR(16) DEFAULT 'proximity', -- proximity, faction, private, global
    faction_id INTEGER REFERENCES factions(id) ON DELETE SET NULL,
    position_x FLOAT,
    position_y FLOAT,
    position_z FLOAT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_chat_sender ON chat_logs(sender_id);
CREATE INDEX idx_chat_receiver ON chat_logs(receiver_id);
CREATE INDEX idx_chat_faction ON chat_logs(faction_id);
CREATE INDEX idx_chat_type ON chat_logs(chat_type);
CREATE INDEX idx_chat_created ON chat_logs(created_at DESC);

-- ==================== Views for Common Queries ====================
CREATE VIEW active_characters AS
SELECT c.*, f.name as faction_name, a.username
FROM characters c
LEFT JOIN factions f ON c.faction_id = f.id
LEFT JOIN accounts a ON c.account_id = a.id
WHERE c.last_seen > NOW() - INTERVAL '1 hour';

CREATE VIEW character_inventory_full AS
SELECT ci.*, i.name, i.item_type, i.weight, i.max_stack, i.description
FROM character_inventory ci
JOIN items i ON ci.item_id = i.id;
