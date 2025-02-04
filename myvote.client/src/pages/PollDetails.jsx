import React, { useState, useEffect } from 'react';
import { useParams } from "react-router-dom";
import { useUser } from '../contexts/UserContext';
import './PollDetails.css';

const PollDetails = () => {
    const { pollId } = useParams();
    //console.log("poll id:", pollId);
    const [poll, setPoll] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const { userId } = useUser();

    const API_BASE_URL = window.location.hostname === "localhost"
        ? "https://localhost:7054/api"
        : "https://myvote-a3cthpgyajgue4c9.canadacentral-01.azurewebsites.net/api";

    useEffect(() => {
        const fetchPoll = async () => {
            try {
                const response = await fetch(`${API_BASE_URL}/poll/${pollId}`);
                if (!response.ok) {
                    throw new Error(`Failed to fetch poll data. Status: ${response.status}`);
                }
                
                const data = await response.json();
                

                setPoll(data);
                setLoading(false);
            } catch (err) {
                
                setError(err.message);
                setLoading(false);
            }
        };

        fetchPoll();
    }, [pollId]);

    const handleVote = async (choiceId) => {
        try {
            const responseBody = {
                choiceId: choiceId,
                userId: userId
            };
            const response = await fetch(`${API_BASE_URL}/vote`, {
                method: "PATCH",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(responseBody)
            });

            if (!response.ok) {
                throw new Error(`Vote submission failed. Status: ${response.status}`);
            }

            alert("Vote submitted successfully!");
        } catch (error) {
            
            alert("There was an error submitting your vote.");
        }
    };

    if (loading) return <p>Loading poll...</p>;
    if (error) return <p>Error: {error}</p>;

    return(
        <div className="poll-details-container">
            <div className="poll-details-card">
                <h1 className="poll-title">{poll.title}</h1>
                <p className="poll-desc">{poll.description}</p>
                <p className="poll-limit">Time Remaining: {poll.timeLimit} hours</p>

                {poll.choices.length > 0 ? (
                    poll.choices.map((choice) => (
                        <button className="poll-choice" key={choice.choiceId} onClick={() => handleVote(choice.choiceId)}>
                            {choice.name} 
                            {/* ({choice.numVotes} votes) */}
                        </button>
                    ))
                ) : (
                    <p>No choices available.</p>
                )}
            </div>
            
        </div>
    );
}

export default PollDetails;