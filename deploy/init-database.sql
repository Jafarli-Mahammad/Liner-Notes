\getenv app_password LINER_APPLICATION_PASSWORD
SELECT format('CREATE ROLE liner_app LOGIN PASSWORD %L', :'app_password') \gexec
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
CREATE SCHEMA IF NOT EXISTS identity AUTHORIZATION liner_operator;
GRANT CONNECT ON DATABASE liner_notes TO liner_app;
GRANT USAGE ON SCHEMA public, identity TO liner_app;
ALTER DEFAULT PRIVILEGES FOR ROLE liner_operator IN SCHEMA public, identity
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO liner_app;
ALTER DEFAULT PRIVILEGES FOR ROLE liner_operator IN SCHEMA public, identity
    GRANT USAGE, SELECT ON SEQUENCES TO liner_app;
