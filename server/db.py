from sqlalchemy import create_engine, Column, Integer, String, ForeignKey, Boolean, text, JSON
from sqlalchemy.orm import sessionmaker, declarative_base, relationship
import pymysql

connection = pymysql.connect(
    host="localhost",
    user="root",
    password=""
)

# True für Tests False für normalen Gebrauch
is_test = False

try:
    with connection.cursor() as cursor:
        cursor.execute(f"CREATE DATABASE IF NOT EXISTS chess{'_test' if is_test else ''}")
finally:
    connection.close()

SQLALCHEMY_DATABASE_URL = f"mysql+pymysql://root@localhost:3306/chess{'_test' if is_test else ''}"

engine = create_engine(SQLALCHEMY_DATABASE_URL)
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

Base = declarative_base()

class User(Base):
    __tablename__ = "users"

    id = Column(Integer, primary_key=True)
    name = Column(String(50), unique=True, nullable=False)
    password = Column(String(255), nullable=False)

    white_matches = relationship("Match", foreign_keys="Match.white_id", back_populates="white_player")
    black_matches = relationship("Match", foreign_keys="Match.black_id", back_populates="black_player")



class Match(Base):
    __tablename__ = "matches"

    match_id = Column(Integer, primary_key=True)

    white_id = Column(Integer, ForeignKey("users.id"), nullable=False)
    black_id = Column(Integer, ForeignKey("users.id"), nullable=False)

    game_state = Column(JSON, nullable=False)
    status = Column(String(20), default="active")  # active, finished, aborted
    winner_id = Column(Integer, ForeignKey("users.id"), nullable=True)

    white_player = relationship("User", foreign_keys=[white_id], back_populates="white_matches")
    black_player = relationship("User", foreign_keys=[black_id], back_populates="black_matches")

with engine.connect() as conn:
    conn.execute(text("SET FOREIGN_KEY_CHECKS=0"))
    conn.execute(text("DROP TABLE IF EXISTS score_table"))
    conn.execute(text("DROP TABLE IF EXISTS answer_table"))
    conn.execute(text("DROP TABLE IF EXISTS question_table"))
    conn.execute(text("DROP TABLE IF EXISTS quiz_table"))
    conn.execute(text("SET FOREIGN_KEY_CHECKS=1"))
    conn.commit()

Base.metadata.create_all(bind=engine)
