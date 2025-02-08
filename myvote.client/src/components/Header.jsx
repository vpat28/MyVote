import React from 'react';
import './Header.css';
import { FaUserCircle, FaHome } from 'react-icons/fa';
import { useNavigate } from 'react-router-dom';

function Header() {
    const navigate = useNavigate();

    const handleUserIconClick = () => {
        navigate('/user');
    };

    const handleHomeIconClick = () => {
        navigate('/');
    };

    return (
        <header className="header">
            {/* Left-aligned title */}
            <div className="header-left">
                <h4 className="header-title">MyVote</h4>
            </div>

            {/* Right-aligned icons */}
            <div className="header-right">
                <div className="home-icon">
                    <FaHome size={24} onClick={handleHomeIconClick} />
                </div>
                <div className="header-icon" onClick={handleUserIconClick}>
                    <FaUserCircle size={24} />
                </div>
            </div>
        </header>
    );
}

export default Header;
