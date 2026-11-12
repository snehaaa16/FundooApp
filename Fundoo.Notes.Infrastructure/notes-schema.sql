CREATE TABLE IF NOT EXISTS notes (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    title varchar(300) NOT NULL,
    content text NOT NULL,
    color varchar(50),
    is_pinned boolean NOT NULL DEFAULT false,
    is_archived boolean NOT NULL DEFAULT false,
    is_trashed boolean NOT NULL DEFAULT false,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_notes_user_id ON notes(user_id);

CREATE TABLE IF NOT EXISTS labels (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    name varchar(100) NOT NULL,
    CONSTRAINT uq_labels_user_name UNIQUE(user_id, name)
);

CREATE TABLE IF NOT EXISTS note_labels (
    note_id uuid NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
    label_id uuid NOT NULL REFERENCES labels(id) ON DELETE CASCADE,
    PRIMARY KEY(note_id, label_id)
);
