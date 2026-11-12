-- init.sql
-- Tabla 1: categorías
CREATE TABLE IF NOT EXISTS "Categories" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL UNIQUE
);

-- Tabla 2: items (cada item pertenece a una categoría)
CREATE TABLE IF NOT EXISTS "Items" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "Price" NUMERIC(12,2) NOT NULL DEFAULT 0,
    "Stock" INTEGER NOT NULL DEFAULT 0,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "CategoryId" INTEGER NOT NULL REFERENCES "Categories"("Id")
);

-- Tabla 3: movimientos de stock (cada movimiento pertenece a un item)
CREATE TABLE IF NOT EXISTS "StockMovements" (
    "Id" SERIAL PRIMARY KEY,
    "ItemId" INTEGER NOT NULL REFERENCES "Items"("Id") ON DELETE CASCADE,
    "Quantity" INTEGER NOT NULL,
    "Reason" VARCHAR(200),
    "Date" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Inserción de datos iniciales de prueba (Seeding)
INSERT INTO "Categories" ("Name") VALUES
('Hardware'),
('Periféricos'),
('Redes');

INSERT INTO "Items" ("Name", "Price", "Stock", "CreatedAt", "CategoryId") VALUES
('Procesador', 250000, 8, NOW(), 1),
('Memoria RAM', 65000, 15, NOW(), 1),
('Disco rígido HDD', 90000, 4, NOW(), 1),
('Fuente de alimentación', 80000, 6, NOW(), 1),
('Teclado mecánico', 45000, 10, NOW(), 2),
('Mouse inalámbrico', 18000, 3, NOW(), 2),
('Router Wi-Fi', 120000, 5, NOW(), 3);

INSERT INTO "StockMovements" ("ItemId", "Quantity", "Reason", "Date") VALUES
(1, 8, 'Stock inicial', NOW()),
(2, 15, 'Stock inicial', NOW()),
(3, 4, 'Stock inicial', NOW()),
(4, 6, 'Stock inicial', NOW()),
(5, 10, 'Stock inicial', NOW()),
(6, 3, 'Stock inicial', NOW()),
(7, 5, 'Stock inicial', NOW());